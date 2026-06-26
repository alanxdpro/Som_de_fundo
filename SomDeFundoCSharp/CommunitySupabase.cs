using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SomDeFundoCSharp;

public sealed class CommunitySupabaseClient : IDisposable
{
    public const int MaxCommunityMusics = 800;
    public const long MaxCommunityUploadBytes = 60L * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly string _url;
    private readonly string _key;
    private readonly string _bucket;
    private readonly string _sessionPath;
    private readonly HttpClient _http = new();
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _realtimeCancellation;
    private Task? _realtimeTask;
    private CommunitySession? _session;
    private int _realtimeRef;

    public CommunitySupabaseClient(string url, string key, string bucket, string sessionPath)
    {
        _url = url.TrimEnd('/');
        _key = key;
        _bucket = string.IsNullOrWhiteSpace(bucket) ? "online-audios" : bucket;
        _sessionPath = sessionPath;
    }

    public string UserId => _session?.User?.Id ?? "";
    public string AccessToken => _session?.AccessToken ?? "";

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_session is not null && !IsExpired(_session))
        {
            return;
        }

        _session = LoadSession();
        if (_session is not null && IsExpired(_session) && !string.IsNullOrWhiteSpace(_session.RefreshToken))
        {
            _session = await RefreshSessionAsync(_session.RefreshToken, cancellationToken);
            SaveSession(_session);
        }

        if (_session is null || IsExpired(_session))
        {
            _session = await SignInAnonymouslyAsync(cancellationToken);
            SaveSession(_session);
        }
    }

    public async Task<List<CommunityMusic>> GetMusicsAsync(string search, int page, bool adminView, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        int offset = Math.Max(0, page) * 50;
        string query = "select=*&order=votos.desc,criado_em.desc&limit=50&offset=" + offset;
        if (!string.IsNullOrWhiteSpace(search))
        {
            string escaped = EscapePostgrestLike(search.Trim());
            query += "&or=" + Uri.EscapeDataString($"(titulo.ilike.*{escaped}*,artista.ilike.*{escaped}*)");
        }

        return await SendJsonAsync<List<CommunityMusic>>(HttpMethod.Get, $"/rest/v1/musicas?{query}", null, cancellationToken) ?? [];
    }

    public async Task<List<CommunityVote>> GetMyVotesAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        try
        {
            return await SendJsonAsync<List<CommunityVote>>(HttpMethod.Get, "/rest/v1/votos?select=musica_id", null, cancellationToken) ?? [];
        }
        catch (InvalidOperationException ex) when (IsMissingTableError(ex, "votos"))
        {
            return [];
        }
    }

    public async Task<bool> IsAdminAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        string userId = Uri.EscapeDataString(UserId);
        List<CommunityAdminUser>? rows = await SendJsonAsync<List<CommunityAdminUser>>(HttpMethod.Get, $"/rest/v1/admin_users?select=user_id&user_id=eq.{userId}&limit=1", null, cancellationToken);
        return rows?.Count > 0;
    }

    public async Task VoteAsync(string musicId, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        var body = new { musica_id = musicId };
        await SendJsonAsync<JsonElement>(HttpMethod.Post, "/rest/v1/votos", body, cancellationToken, prefer: "return=minimal");
    }

    public async Task UploadMusicAsync(NewCommunityMusic music, string filePath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        string extension = Path.GetExtension(filePath).ToLowerInvariant();
        string storagePath = $"{UserId}/{DateTime.UtcNow:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}{extension}";
        await UploadStorageAsync(storagePath, filePath, progress, cancellationToken);

        var body = new
        {
            titulo = music.Title,
            artista = music.Artist,
            enviado_por = music.UploadedBy,
            observacao = music.Note,
            arquivo_url = storagePath,
            duracao_segundos = music.DurationSeconds,
            tamanho_bytes = music.SizeBytes
        };

        await SendJsonAsync<JsonElement>(HttpMethod.Post, "/rest/v1/musicas", body, cancellationToken, prefer: "return=minimal");
    }

    public async Task DownloadMusicAsync(CommunityMusic music, string localPath, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        string storagePath = Uri.EscapeDataString(music.StoragePath).Replace("%2F", "/");
        using var request = CreateRequest(HttpMethod.Get, $"/storage/v1/object/authenticated/{Uri.EscapeDataString(_bucket)}/{storagePath}");
        using HttpResponseMessage response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureSuccessAsync(response);

        await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using Stream target = File.Create(localPath);
        long? total = response.Content.Headers.ContentLength;
        var buffer = new byte[81920];
        long copied = 0;
        int read;
        while ((read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            copied += read;
            if (total > 0)
            {
                progress?.Report(copied * 100.0 / total.Value);
            }
        }

        await SendJsonAsync<JsonElement>(HttpMethod.Post, "/rest/v1/rpc/incrementar_download_musica", new { musica_id_param = music.Id }, cancellationToken, prefer: "return=minimal");
    }

    public async Task ApproveMusicAsync(string musicId, CancellationToken cancellationToken = default)
    {
        await PatchMusicAsync(musicId, new { aprovado = true }, cancellationToken);
    }

    public async Task DeleteMusicAsync(CommunityMusic music, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        await DeleteStorageObjectAsync(music.StoragePath, cancellationToken);
        await SendJsonAsync<JsonElement>(HttpMethod.Delete, $"/rest/v1/musicas?id=eq.{Uri.EscapeDataString(music.Id)}", null, cancellationToken, prefer: "return=minimal");
    }

    public async Task<CommunityCleanupResult> RunCleanupAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        List<CommunityMusic> all = await LoadAllAdminMusicsAsync(cancellationToken);
        var toRemove = new Dictionary<string, CommunityMusic>(StringComparer.OrdinalIgnoreCase);

        foreach (CommunityMusic music in all.Where(m => m.Votes == 0 && m.Downloads == 0 && m.CreatedAt < DateTimeOffset.UtcNow.AddDays(-30)))
        {
            toRemove[music.Id] = music;
        }

        if (all.Count > MaxCommunityMusics)
        {
            foreach (CommunityMusic music in all.OrderBy(m => m.Votes).ThenBy(m => m.Downloads).ThenBy(m => m.CreatedAt).Take(all.Count - MaxCommunityMusics))
            {
                toRemove[music.Id] = music;
            }
        }

        int removed = 0;
        foreach (CommunityMusic music in toRemove.Values)
        {
            progress?.Report($"Removendo {music.Title}");
            await DeleteMusicAsync(music, cancellationToken);
            removed++;
        }

        return new CommunityCleanupResult(all.Count, removed, all.Sum(music => music.SizeBytes));
    }

    public async Task<CommunityStats> GetAdminStatsAsync(CancellationToken cancellationToken = default)
    {
        await InitializeAsync(cancellationToken);
        List<CommunityMusic> all = await LoadAllAdminMusicsAsync(cancellationToken);
        return new CommunityStats(all.Count, all.Sum(music => music.SizeBytes), MaxCommunityMusics);
    }

    public void StartRealtime(Func<Task> onChange)
    {
        StopRealtime();
        _realtimeCancellation = new CancellationTokenSource();
        _realtimeTask = Task.Run(() => RealtimeLoopAsync(onChange, _realtimeCancellation.Token));
    }

    public void StopRealtime()
    {
        _realtimeCancellation?.Cancel();
        _realtimeCancellation?.Dispose();
        _realtimeCancellation = null;
        _socket?.Abort();
        _socket?.Dispose();
        _socket = null;
    }

    private async Task<List<CommunityMusic>> LoadAllAdminMusicsAsync(CancellationToken cancellationToken)
    {
        var result = new List<CommunityMusic>();
        for (int page = 0; ; page++)
        {
            int offset = page * 1000;
            string query = $"select=*&order=votos.asc,downloads.asc,criado_em.asc&limit=1000&offset={offset}";
            List<CommunityMusic>? batch = await SendJsonAsync<List<CommunityMusic>>(HttpMethod.Get, $"/rest/v1/musicas?{query}", null, cancellationToken);
            if (batch is null || batch.Count == 0)
            {
                break;
            }

            result.AddRange(batch);
            if (batch.Count < 1000)
            {
                break;
            }
        }

        return result;
    }

    private async Task PatchMusicAsync(string musicId, object patch, CancellationToken cancellationToken)
    {
        await InitializeAsync(cancellationToken);
        await SendJsonAsync<JsonElement>(new HttpMethod("PATCH"), $"/rest/v1/musicas?id=eq.{Uri.EscapeDataString(musicId)}", patch, cancellationToken, prefer: "return=minimal");
    }

    private async Task UploadStorageAsync(string storagePath, string filePath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        string path = Uri.EscapeDataString(storagePath).Replace("%2F", "/");
        using var request = CreateRequest(HttpMethod.Post, $"/storage/v1/object/{Uri.EscapeDataString(_bucket)}/{path}");
        request.Headers.TryAddWithoutValidation("x-upsert", "false");
        request.Content = new ProgressStreamContent(File.OpenRead(filePath), MimeFromExtension(filePath), progress);
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
    }

    private async Task DeleteStorageObjectAsync(string storagePath, CancellationToken cancellationToken)
    {
        using var request = CreateRequest(HttpMethod.Delete, $"/storage/v1/object/{Uri.EscapeDataString(_bucket)}");
        request.Content = JsonContent(new { prefixes = new[] { storagePath } });
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            await EnsureSuccessAsync(response);
        }
    }

    private async Task<CommunitySession> SignInAnonymouslyAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/auth/v1/signup");
        request.Headers.TryAddWithoutValidation("apikey", _key);
        request.Content = JsonContent(new { });
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        return (await ReadJsonAsync<CommunitySession>(response, cancellationToken))!;
    }

    private async Task<CommunitySession?> RefreshSessionAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_url}/auth/v1/token?grant_type=refresh_token");
        request.Headers.TryAddWithoutValidation("apikey", _key);
        request.Content = JsonContent(new { refresh_token = refreshToken });
        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await ReadJsonAsync<CommunitySession>(response, cancellationToken);
    }

    private async Task<T?> SendJsonAsync<T>(HttpMethod method, string relativeUrl, object? body, CancellationToken cancellationToken, string? prefer = null)
    {
        using var request = CreateRequest(method, relativeUrl);
        if (prefer is not null)
        {
            request.Headers.TryAddWithoutValidation("Prefer", prefer);
        }

        if (body is not null)
        {
            request.Content = JsonContent(body);
        }

        using HttpResponseMessage response = await _http.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response);
        if (response.Content.Headers.ContentLength == 0)
        {
            return default;
        }

        return await ReadJsonAsync<T>(response, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, $"{_url}{relativeUrl}");
        request.Headers.TryAddWithoutValidation("apikey", _key);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", string.IsNullOrWhiteSpace(AccessToken) ? _key : AccessToken);
        return request;
    }

    private static HttpContent JsonContent(object value)
    {
        return new StringContent(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        string body = await response.Content.ReadAsStringAsync();
        string message = $"{(int)response.StatusCode} {response.ReasonPhrase}: {body}";
        if (body.Contains("anonymous_provider_disabled", StringComparison.OrdinalIgnoreCase)
            || body.Contains("Anonymous sign-ins are disabled", StringComparison.OrdinalIgnoreCase))
        {
            message =
                "Login anonimo do Supabase esta desativado para este projeto. " +
                "Ative Authentication > Sign In / Providers > Anonymous Sign-Ins no mesmo projeto da SUPABASE_URL usada pelo app. " +
                $"Detalhes: {message}";
        }
        else if (body.Contains("PGRST205", StringComparison.OrdinalIgnoreCase)
            && body.Contains("Could not find the table", StringComparison.OrdinalIgnoreCase))
        {
            message =
                "Tabela da biblioteca online nao encontrada no Supabase. " +
                "Rode o SQL atualizado em Biblioteca Online > Supabase SQL > Copiar SQL e aguarde alguns segundos para o schema cache atualizar. " +
                $"Detalhes: {message}";
        }

        throw new InvalidOperationException(message);
    }

    private static bool IsMissingTableError(Exception ex, string tableName)
    {
        string message = ex.ToString();
        return message.Contains("PGRST205", StringComparison.OrdinalIgnoreCase)
            && message.Contains($"public.{tableName}", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RealtimeLoopAsync(Func<Task> onChange, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await InitializeAsync(cancellationToken);
                string projectHost = new Uri(_url).Host;
                _socket = new ClientWebSocket();
                await _socket.ConnectAsync(new Uri($"wss://{projectHost}/realtime/v1/websocket?apikey={Uri.EscapeDataString(_key)}&vsn=1.0.0"), cancellationToken);
                await SendRealtimeAsync("realtime:community-musicas", "phx_join", new
                {
                    config = new
                    {
                        postgres_changes = new[]
                        {
                            new { @event = "*", schema = "public", table = "musicas" },
                            new { @event = "*", schema = "public", table = "votos" }
                        },
                        broadcast = new { self = false },
                        presence = new { key = "" }
                    },
                    access_token = AccessToken
                }, cancellationToken);

                using var heartbeat = new PeriodicTimer(TimeSpan.FromSeconds(25));
                Task receive = ReceiveRealtimeAsync(onChange, cancellationToken);
                while (!receive.IsCompleted && await heartbeat.WaitForNextTickAsync(cancellationToken))
                {
                    await SendRealtimeAsync("phoenix", "heartbeat", new { }, cancellationToken);
                }
            }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
            }
        }
    }

    private async Task ReceiveRealtimeAsync(Func<Task> onChange, CancellationToken cancellationToken)
    {
        var buffer = new byte[32768];
        while (_socket?.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await _socket.ReceiveAsync(buffer, cancellationToken);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return;
                }

                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);

            string json = Encoding.UTF8.GetString(ms.ToArray());
            if (json.Contains("\"postgres_changes\"", StringComparison.OrdinalIgnoreCase)
                || json.Contains("\"table\":\"musicas\"", StringComparison.OrdinalIgnoreCase)
                || json.Contains("\"table\":\"votos\"", StringComparison.OrdinalIgnoreCase))
            {
                await onChange();
            }
        }
    }

    private async Task SendRealtimeAsync(string topic, string eventName, object payload, CancellationToken cancellationToken)
    {
        if (_socket is null)
        {
            return;
        }

        string message = JsonSerializer.Serialize(new
        {
            topic,
            @event = eventName,
            payload,
            @ref = Interlocked.Increment(ref _realtimeRef).ToString(),
            join_ref = "1"
        }, JsonOptions);
        byte[] bytes = Encoding.UTF8.GetBytes(message);
        await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, cancellationToken);
    }

    private CommunitySession? LoadSession()
    {
        try
        {
            if (!File.Exists(_sessionPath))
            {
                return null;
            }

            return JsonSerializer.Deserialize<CommunitySession>(File.ReadAllText(_sessionPath), JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    private void SaveSession(CommunitySession? session)
    {
        if (session is null)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_sessionPath)!);
        session.SavedAt = DateTimeOffset.UtcNow;
        File.WriteAllText(_sessionPath, JsonSerializer.Serialize(session, JsonOptions));
    }

    private static bool IsExpired(CommunitySession session)
    {
        return session.SavedAt.AddSeconds(Math.Max(60, session.ExpiresIn - 60)) <= DateTimeOffset.UtcNow;
    }

    private static string EscapePostgrestLike(string value)
    {
        return value.Replace("*", "\\*", StringComparison.Ordinal).Replace(",", "\\,", StringComparison.Ordinal);
    }

    private static string MimeFromExtension(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".wav" => "audio/wav",
            ".m4a" => "audio/mp4",
            _ => "audio/mpeg"
        };
    }

    public void Dispose()
    {
        StopRealtime();
        _http.Dispose();
    }
}

public sealed class ProgressStreamContent : HttpContent
{
    private readonly Stream _source;
    private readonly IProgress<double>? _progress;

    public ProgressStreamContent(Stream source, string contentType, IProgress<double>? progress)
    {
        _source = source;
        _progress = progress;
        Headers.ContentType = new MediaTypeHeaderValue(contentType);
        Headers.ContentLength = source.Length;
    }

    protected override async Task SerializeToStreamAsync(Stream stream, TransportContext? context)
    {
        var buffer = new byte[81920];
        long total = _source.Length;
        long sent = 0;
        int read;
        while ((read = await _source.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
        {
            await stream.WriteAsync(buffer.AsMemory(0, read));
            sent += read;
            _progress?.Report(sent * 100.0 / total);
        }
    }

    protected override bool TryComputeLength(out long length)
    {
        length = _source.Length;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _source.Dispose();
        }

        base.Dispose(disposing);
    }
}

public sealed class CommunitySession
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = "";

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = "";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("user")]
    public CommunityUser? User { get; set; }

    public DateTimeOffset SavedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class CommunityUser
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";
}

public sealed class CommunityMusic
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("titulo")]
    public string Title { get; set; } = "";

    [JsonPropertyName("artista")]
    public string Artist { get; set; } = "";

    [JsonPropertyName("enviado_por")]
    public string UploadedBy { get; set; } = "";

    [JsonPropertyName("observacao")]
    public string? Note { get; set; }

    [JsonPropertyName("arquivo_url")]
    public string FileUrl { get; set; } = "";

    [JsonPropertyName("storage_path")]
    public string? StoragePathValue { get; set; }

    [JsonIgnore]
    public string StoragePath => string.IsNullOrWhiteSpace(StoragePathValue) ? FileUrl : StoragePathValue;

    [JsonPropertyName("duracao_segundos")]
    public int DurationSeconds { get; set; }

    [JsonPropertyName("tamanho_bytes")]
    public long SizeBytes { get; set; }

    [JsonPropertyName("votos")]
    public int Votes { get; set; }

    [JsonPropertyName("downloads")]
    public int Downloads { get; set; }

    [JsonPropertyName("aprovado")]
    public bool Approved { get; set; }

    [JsonPropertyName("criado_em")]
    public DateTimeOffset CreatedAt { get; set; }

    [JsonPropertyName("criado_por")]
    public string CreatedBy { get; set; } = "";
}

public sealed class CommunityVote
{
    [JsonPropertyName("musica_id")]
    public string MusicId { get; set; } = "";
}

public sealed class CommunityAdminUser
{
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = "";
}

public sealed record NewCommunityMusic(string Title, string Artist, string UploadedBy, string? Note, int DurationSeconds, long SizeBytes);

public sealed record CommunityStats(int TotalMusics, long TotalBytes, int MaxMusics);

public sealed record CommunityCleanupResult(int TotalBefore, int Removed, long TotalBytesBefore);
