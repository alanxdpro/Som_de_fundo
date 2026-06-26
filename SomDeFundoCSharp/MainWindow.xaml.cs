using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.Dsp;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using QRCoder;

namespace SomDeFundoCSharp;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private static readonly string AppDataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SomDeFundoPro");
    private static readonly string LocalSoundsDirectory = Path.Combine(AppDataDirectory, "sons");
    private static readonly string LocalCoversDirectory = Path.Combine(AppDataDirectory, "capas");
    private static readonly string LocalBackupsDirectory = Path.Combine(AppDataDirectory, "backups");
    private static readonly string LocalLogsDirectory = Path.Combine(AppDataDirectory, "logs");
    private static readonly string LocalAiDirectory = Path.Combine(AppDataDirectory, "IA");
    private static readonly string LocalSettingsPath = Path.Combine(AppDataDirectory, "config.json");
    private static readonly string LocalOnlineDirectory = Path.Combine(LocalSoundsDirectory, "online");
    private static readonly string LocalCommunityMusicDirectory = Path.Combine(AppDataDirectory, "Biblioteca", "Musicas");
    private static readonly string LocalCommunitySessionPath = Path.Combine(AppDataDirectory, "community-session.json");
    private const long MaxCommunityUploadBytes = CommunitySupabaseClient.MaxCommunityUploadBytes;
    private const int RemotePort = 5005;
    private const string SupabaseUrlKey = "SOM_DE_FUNDO_SUPABASE_URL";
    private const string SupabasePublicKeyKey = "SOM_DE_FUNDO_SUPABASE_PUBLIC_KEY";
    private const string SupabaseAnonKeyAlias = "SUPABASE_ANON_KEY";
    private const string SupabaseBucketKey = "SOM_DE_FUNDO_SUPABASE_BUCKET";
    private const long MaxRemoteUploadBytes = 800L * 1024 * 1024;
    private const long RemoteUploadSlowWarningBytes = 40L * 1024 * 1024;
    private const int MaxRemoteHeaderBytes = 64 * 1024;
    private const int MaxAutomaticBackups = 20;
    private const string ManualUpdateUrl = "";
    private static readonly Lazy<Dictionary<string, string>> DotEnvValues = new(LoadDotEnvValues);
    private static readonly Lazy<Dictionary<string, string>> DotEnvValueSources = new(LoadDotEnvValueSources);
    private const string DefaultPlaylistName = "Default";
    private static readonly string[] DefaultPadColors =
    [
        "#2563EB", "#7C3AED", "#0891B2", "#059669", "#E11D48",
        "#DC2626", "#DB2777", "#0F766E", "#EA580C", "#4F46E5",
        "#0EA5E9", "#9333EA", "#14B8A6", "#F43F5E", "#84CC16",
        "#6366F1", "#F59E0B", "#10B981", "#EF4444", "#3B82F6"
    ];
    private const string SupabaseSchemaSql = """
-- Sistema de Sugestao de Musicas da Comunidade - Som de Fundo Pro
-- Rode no SQL Editor do Supabase. Nunca coloque service_role no app desktop.
-- Depois de entrar no app uma vez, copie seu User ID na area Admin e cadastre:
-- insert into public.admin_users (user_id) values ('SEU_USER_ID');

create extension if not exists pgcrypto;

create table if not exists public.musicas (
  id uuid primary key default gen_random_uuid(),
  titulo text not null check (char_length(titulo) between 1 and 120),
  artista text not null default '',
  enviado_por text not null default 'Anonimo' check (char_length(enviado_por) between 1 and 80),
  observacao text,
  arquivo_url text not null,
  storage_path text generated always as (arquivo_url) stored,
  duracao_segundos int not null check (duracao_segundos between 0 and 7200),
  tamanho_bytes bigint not null check (tamanho_bytes between 1 and 62914560),
  votos int not null default 0,
  downloads int not null default 0,
  aprovado boolean not null default false,
  criado_em timestamptz not null default now(),
  criado_por uuid not null default auth.uid()
);

alter table public.musicas add column if not exists enviado_por text not null default 'Anonimo';
do $$
begin
  alter table public.musicas drop constraint if exists musicas_tamanho_bytes_check;
  alter table public.musicas add constraint musicas_tamanho_bytes_check check (tamanho_bytes between 1 and 62914560);
  alter table public.musicas drop constraint if exists musicas_duracao_segundos_check;
  alter table public.musicas add constraint musicas_duracao_segundos_check check (duracao_segundos between 0 and 7200);
exception when duplicate_object then null;
end $$;

create table if not exists public.votos (
  id uuid primary key default gen_random_uuid(),
  musica_id uuid not null references public.musicas(id) on delete cascade,
  user_id uuid not null default auth.uid(),
  criado_em timestamptz not null default now(),
  unique (musica_id, user_id)
);

create table if not exists public.admin_users (
  user_id uuid primary key,
  criado_em timestamptz not null default now()
);

do $$
begin
  if exists (select 1 from information_schema.tables where table_schema = 'public' and table_name = 'online_audios') then
    execute $migration$
      insert into public.musicas (id, titulo, artista, observacao, arquivo_url, duracao_segundos, tamanho_bytes, votos, downloads, aprovado, criado_em, criado_por)
      select id, name, category, null, storage_path, duration_seconds, 1, greatest(likes - dislikes, 0), 0, is_approved, created_at, '00000000-0000-0000-0000-000000000000'::uuid
      from public.online_audios
      on conflict (id) do nothing
    $migration$;
  end if;
end $$;

create index if not exists musicas_ranking_idx on public.musicas (votos desc, criado_em desc);
create index if not exists musicas_busca_idx on public.musicas (lower(titulo), lower(artista));
create index if not exists musicas_limpeza_idx on public.musicas (votos, downloads, criado_em);
create index if not exists votos_user_idx on public.votos (user_id);

create or replace function public.is_admin()
returns boolean
language sql
stable
security definer
set search_path = public
as $$
  select exists (select 1 from public.admin_users where user_id = auth.uid());
$$;

create or replace function public.incrementar_download_musica(musica_id_param uuid)
returns void
language plpgsql
security invoker
set search_path = public
as $$
begin
  update public.musicas
  set downloads = downloads + 1
  where id = musica_id_param;
end;
$$;

create or replace function public.incrementar_votos_musica()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
  update public.musicas
  set votos = votos + 1
  where id = new.musica_id;
  return new;
end;
$$;

drop trigger if exists votos_incrementar_musica on public.votos;
create trigger votos_incrementar_musica
after insert on public.votos
for each row execute function public.incrementar_votos_musica();

alter table public.musicas enable row level security;
alter table public.votos enable row level security;
alter table public.admin_users enable row level security;

drop policy if exists musicas_select_comunidade on public.musicas;
create policy musicas_select_comunidade on public.musicas
for select to authenticated
using (true);

drop policy if exists musicas_insert_propria on public.musicas;
create policy musicas_insert_propria on public.musicas
for insert to authenticated
with check (criado_por = auth.uid() and tamanho_bytes <= 62914560 and duracao_segundos <= 7200);

drop policy if exists musicas_update_admin on public.musicas;
create policy musicas_update_admin on public.musicas
for update to authenticated
using (public.is_admin())
with check (public.is_admin());

drop policy if exists musicas_delete_admin on public.musicas;
create policy musicas_delete_admin on public.musicas
for delete to authenticated
using (public.is_admin());

drop policy if exists votos_select_proprio_ou_admin on public.votos;
create policy votos_select_proprio_ou_admin on public.votos
for select to authenticated
using (user_id = auth.uid() or public.is_admin());

drop policy if exists votos_insert_proprio on public.votos;
create policy votos_insert_proprio on public.votos
for insert to authenticated
with check (user_id = auth.uid());

drop policy if exists admin_users_select_self on public.admin_users;
create policy admin_users_select_self on public.admin_users
for select to authenticated
using (user_id = auth.uid());

grant usage on schema public to anon, authenticated;
grant select, insert on public.musicas to authenticated;
grant select, insert on public.votos to authenticated;
grant select on public.admin_users to authenticated;
grant update, delete on public.musicas to authenticated;
grant execute on function public.incrementar_download_musica(uuid) to authenticated;

do $$
begin
  alter publication supabase_realtime add table public.musicas;
exception when duplicate_object then null;
end $$;

do $$
begin
  alter publication supabase_realtime add table public.votos;
exception when duplicate_object then null;
end $$;

insert into storage.buckets (id, name, public, file_size_limit, allowed_mime_types)
values ('online-audios', 'online-audios', false, 62914560, array['audio/mpeg', 'audio/wav', 'audio/mp4', 'audio/x-m4a'])
on conflict (id) do update set public = false, file_size_limit = 62914560, allowed_mime_types = excluded.allowed_mime_types;

drop policy if exists community_storage_upload_own on storage.objects;
create policy community_storage_upload_own on storage.objects
for insert to authenticated
with check (bucket_id = 'online-audios' and (storage.foldername(name))[1] = auth.uid()::text);

drop policy if exists community_storage_read_visible on storage.objects;
create policy community_storage_read_visible on storage.objects
for select to authenticated
using (
  bucket_id = 'online-audios'
  and exists (
    select 1 from public.musicas m
    where m.arquivo_url = storage.objects.name
  )
);

drop policy if exists community_storage_delete_admin on storage.objects;
create policy community_storage_delete_admin on storage.objects
for delete to authenticated
using (bucket_id = 'online-audios' and public.is_admin());
""";

    private readonly ObservableCollection<PadCard> _pads = [];
    private readonly ObservableCollection<OnlineAudio> _topAudios = [];
    private readonly ObservableCollection<OnlineAudio> _adminAudios = [];
    private readonly List<PlaylistState> _playlists = [];
    private readonly AudioDeck _deckA = new();
    private readonly AudioDeck _deckB = new();
    private readonly AudioDeck _previewDeck = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly MixingSampleProvider _mainMixer = new(WaveFormat.CreateIeeeFloatWaveFormat(44100, 2)) { ReadFully = true };
    private readonly WaveOutEvent _mainOutput = new();

    private AudioDeck _activeDeck = null!;
    private AudioDeck _standbyDeck = null!;
    private AudioDeck? _fadeInDeck;
    private AudioDeck? _fadeOutDeck;
    private PadCard? _currentPad;
    private PadCard? _fadeOutPad;
    private PadCard? _editingPad;
    private bool _isPaused;
    private bool _stopWhenFadeEnds;
    private bool _suppressPlaybackStopped;
    private CancellationTokenSource? _fadeCancellation;
    private int _gridColumns = 5;
    private double _basePadHeight = 140;
    private double _scalePercent = 100;
    private int _crossfadeSeconds = 3;
    private string _editorColor = "#2563EB";
    private string? _editorSoundPath;
    private string? _editorCoverPath;
    private bool _editorLoop;
    private bool _returnToEditorAfterAi;
    private CancellationTokenSource? _aiCancellation;
    private string? _previewCommunityAudioId;
    private bool _loadingEqualizer;
    private bool _eqEnabled;
    private double _eqBass;
    private double _eqMid;
    private double _eqTreble;
    private string _eqPreset = "padrao";
    private TcpListener? _remoteServer;
    private CancellationTokenSource? _remoteCancellation;
    private int _remoteConnections;
    private readonly object _remoteUploadLock = new();
    private readonly HashSet<string> _remoteUploadKeys = [];
    private readonly Dictionary<string, RemoteUploadStatus> _remoteUploadStatuses = [];
    private readonly CommunitySupabaseClient _communityClient;
    private string? _communityUploadPath;
    private int _communityPage;
    private int _adminPage;
    private int _currentPlaylistIndex;
    private bool _updatingPlaylistSelector;
    private bool _isCommunityAdmin;
    private bool _loadingLibrary;
    private bool _firstRunCompleted;
    private bool _loadedExistingSettings;

    public event PropertyChangedEventHandler? PropertyChanged;

    private static bool IsAdminBuild
    {
        get
        {
#if ADMIN_APP
            return true;
#else
            return false;
#endif
        }
    }

    public int GridColumns
    {
        get => _gridColumns;
        set => SetField(ref _gridColumns, value);
    }

    public double PadHeight
    {
        get => _basePadHeight * (_scalePercent / 100.0);
        private set => OnPropertyChanged();
    }

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;
        LogSupabaseStartupConfig();
        _communityClient = new CommunitySupabaseClient(GetSupabaseUrl(), GetSupabasePublicKey(), GetSupabaseBucket(), LocalCommunitySessionPath);

        BuildPads();
        BuildLibrary();
        LoadLocalSettings();
        if (_playlists.Count == 0)
        {
            InitializePlaylists();
        }
        else
        {
            _currentPlaylistIndex = Math.Clamp(_currentPlaylistIndex, 0, _playlists.Count - 1);
            ApplyCurrentPlaylist();
        }
        EnsureCommunitySenderName();
        ConfigureBuildModeUi();

        PadsList.ItemsSource = _pads;
        TopAudiosList.ItemsSource = _topAudios;
        VotingAudiosList.ItemsSource = _adminAudios;

        _activeDeck = _deckA;
        _standbyDeck = _deckB;
        _deckA.PlaybackStopped += Deck_PlaybackStopped;
        _deckB.PlaybackStopped += Deck_PlaybackStopped;
        _mainOutput.Init(_mainMixer);
        _mainOutput.Play();
        _timer.Tick += Timer_Tick;
        KeyDown += MainWindow_KeyDown;

        ApplyEffectiveVolume();
        RefreshLibraryFilter();
        UpdateLibraryPanel();
        UpdateSupabaseSqlBox();
        StartRemoteServer(showError: false);
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshAllPadAudioStatus();
        if (!_loadedExistingSettings || !_firstRunCompleted)
        {
            ShowFirstRunOverlay();
        }
    }

    private void BuildPads()
    {
        for (int i = 1; i <= 20; i++)
        {
            var pad = new PadCard
            {
                Id = i,
                Name = $"Botao {i}",
                Color = DefaultPadColors[i - 1],
                Volume = i == 1 ? 0 : 100
            };
            pad.PropertyChanged += Pad_PropertyChanged;
            _pads.Add(pad);
        }
    }

    private void BuildLibrary()
    {
        _topAudios.Clear();
        _adminAudios.Clear();
    }

    private void ConfigureBuildModeUi()
    {
        if (IsAdminBuild)
        {
            Title = "Som de Fundo Pro Admin";
            return;
        }

        AdminSuggestionsTab.Visibility = Visibility.Collapsed;
        AdminCleanupTab.Visibility = Visibility.Collapsed;
        SupabaseSqlTab.Visibility = Visibility.Collapsed;
        SqlConfigButton.Visibility = Visibility.Collapsed;
        LibraryTabs.SelectedIndex = 0;
    }

    private void Pad_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == _currentPad && e.PropertyName == nameof(PadCard.Volume))
        {
            ApplyEffectiveVolume();
        }
    }

    private void Pad_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not PadCard pad)
        {
            return;
        }

        pad.RefreshAudioStatus();

        if (_currentPad == pad)
        {
            StopPlayback();
            return;
        }

        if (string.IsNullOrWhiteSpace(pad.SoundPath))
        {
            MessageBox.Show("Configure um arquivo de audio neste pad pelo botao de configuracao.", "Pad sem audio", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (pad.SoundPath.StartsWith("offline://", StringComparison.OrdinalIgnoreCase))
        {
            StopPlayback();
            _currentPad = pad;
            pad.IsPlaying = true;
            NowPlayingText.Text = $"{pad.Name} (offline simulado)";
            StatusText.Text = "Tocando - Biblioteca Offline";
            StatusDot.Fill = Brushes.LimeGreen;
            TimeText.Text = "offline";
            PlaybackProgress.Value = 100;
            return;
        }

        if (!File.Exists(pad.SoundPath))
        {
            LocateMissingPadAudio(pad);
            return;
        }

        try
        {
            StartFilePadWithFade(pad);
        }
        catch (Exception ex)
        {
            StopPlayback(useFade: false);
            MessageBox.Show($"Nao foi possivel tocar o audio.\n{ex.Message}", "Erro de audio", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StartFilePadWithFade(PadCard pad)
    {
        if (pad.SoundPath is null)
        {
            return;
        }

        CancelPendingFadeOut();

        AudioDeck oldDeck = _activeDeck;
        PadCard? oldPad = _currentPad;
        AudioDeck newDeck = _standbyDeck;

        newDeck.Close();
        newDeck.Load(pad.SoundPath, _eqEnabled, _eqBass, _eqMid, _eqTreble, _mainMixer);
        newDeck.Volume = 0;

        _activeDeck = newDeck;
        _standbyDeck = oldDeck;
        _currentPad = pad;
        _isPaused = false;
        pad.IsPlaying = true;

        newDeck.Play();
        _timer.Start();

        NowPlayingText.Text = GetMusicDisplayName(pad);
        StatusText.Text = oldPad is null ? "Tocando - Fade In" : "Tocando - Crossfade Ativo";
        StatusDot.Fill = Brushes.LimeGreen;

        BeginFade(newDeck, oldPad is null ? null : oldDeck, oldPad, stopWhenFadeEnds: false);
    }

    private static string GetMusicDisplayName(PadCard pad)
    {
        if (IsRealAudioPath(pad.SoundPath))
        {
            return Path.GetFileNameWithoutExtension(pad.SoundPath) ?? pad.Name;
        }

        return pad.Name;
    }

    private void Deck_PlaybackStopped(object? sender, EventArgs e)
    {
        if (!Dispatcher.CheckAccess())
        {
            _ = Dispatcher.InvokeAsync(() => Deck_PlaybackStopped(sender, e));
            return;
        }

        if (_suppressPlaybackStopped || !ReferenceEquals(sender, _activeDeck))
        {
            return;
        }

        if (_currentPad?.Loop == true)
        {
            _activeDeck.Position = TimeSpan.Zero;
            _activeDeck.Play();
            return;
        }

        StopPlayback(useFade: false);
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        if (!_activeDeck.HasAudio || _activeDeck.Duration.TotalSeconds <= 0)
        {
            return;
        }

        TimeSpan total = _activeDeck.Duration;
        TimeSpan position = _activeDeck.Position;
        double percent = Math.Clamp(position.TotalSeconds / total.TotalSeconds * 100, 0, 100);
        PlaybackProgress.Value = percent;
        TimeText.Text = $"{FormatTime(position)} / {FormatTime(total)}";
    }

    private void PlaybackProgress_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_activeDeck.HasAudio || _activeDeck.Duration.TotalSeconds <= 0)
        {
            return;
        }

        double ratio = Math.Clamp(e.GetPosition(PlaybackProgress).X / Math.Max(1, PlaybackProgress.ActualWidth), 0, 1);
        _activeDeck.Position = TimeSpan.FromSeconds(_activeDeck.Duration.TotalSeconds * ratio);
        Timer_Tick(this, EventArgs.Empty);
    }

    private static string FormatTime(TimeSpan value)
    {
        return $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";
    }

    private void StopButton_Click(object sender, RoutedEventArgs e)
    {
        StopPlayback();
    }

    private void StopPlayback(bool useFade = true)
    {
        if (useFade && _currentPad is not null && _activeDeck.Volume > 0 && _crossfadeSeconds > 0)
        {
            StatusText.Text = "Parando - Fade Out";
            BeginFade(null, _activeDeck, _currentPad, stopWhenFadeEnds: true);
            return;
        }

        StopPlaybackImmediate();
    }

    private void StopPlaybackImmediate()
    {
        CancelActiveFade();
        _suppressPlaybackStopped = true;
        _deckA.Close();
        _deckB.Close();
        _suppressPlaybackStopped = false;
        _fadeInDeck = null;
        _fadeOutDeck = null;
        _fadeOutPad = null;
        _stopWhenFadeEnds = false;
        _timer.Stop();
        foreach (PadCard pad in _pads)
        {
            pad.IsPlaying = false;
        }

        _currentPad = null;
        _isPaused = false;
        NowPlayingText.Text = "Nenhum som ativo";
        StatusText.Text = "Parado - Aguardando Fundo";
        StatusDot.Fill = new SolidColorBrush(Color.FromRgb(51, 65, 85));
        TimeText.Text = "00:00 / 00:00";
        PlaybackProgress.Value = 0;
    }

    private void BeginFade(AudioDeck? fadeInDeck, AudioDeck? fadeOutDeck, PadCard? fadeOutPad, bool stopWhenFadeEnds)
    {
        CancelActiveFade();
        _fadeInDeck = fadeInDeck;
        _fadeOutDeck = fadeOutDeck;
        _fadeOutPad = fadeOutPad;
        _stopWhenFadeEnds = stopWhenFadeEnds;

        if (fadeInDeck is null && fadeOutDeck is null)
        {
            return;
        }

        var cancellation = new CancellationTokenSource();
        _fadeCancellation = cancellation;
        TimeSpan fadeDuration = TimeSpan.FromSeconds(Math.Max(0.1, _crossfadeSeconds));
        double fadeOutStartVolume = fadeOutDeck?.Volume ?? 0;
        _ = RunFadeAsync(fadeInDeck, fadeOutDeck, fadeOutPad, stopWhenFadeEnds, fadeDuration, fadeOutStartVolume, cancellation.Token);
    }

    private async Task RunFadeAsync(
        AudioDeck? fadeInDeck,
        AudioDeck? fadeOutDeck,
        PadCard? fadeOutPad,
        bool stopWhenFadeEnds,
        TimeSpan fadeDuration,
        double fadeOutStartVolume,
        CancellationToken token)
    {
        try
        {
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                token.ThrowIfCancellationRequested();
                double progress = Math.Clamp(stopwatch.Elapsed.TotalMilliseconds / fadeDuration.TotalMilliseconds, 0, 1);
                double fadeOutCurve = Math.Pow(1 - progress, 2.2);
                double fadeInCurve = 1 - Math.Pow(1 - progress, 2.2);

                await Dispatcher.InvokeAsync(() =>
                {
                    if (fadeInDeck is not null)
                    {
                        fadeInDeck.Volume = GetEffectiveVolume(_currentPad) * fadeInCurve;
                    }

                    if (fadeOutDeck is not null)
                    {
                        fadeOutDeck.Volume = fadeOutStartVolume * fadeOutCurve;
                    }
                });

                if (progress >= 1)
                {
                    break;
                }

                await Task.Delay(TimeSpan.FromMilliseconds(15), token);
            }

            await Dispatcher.InvokeAsync(() => CompleteFade(fadeInDeck, fadeOutDeck, fadeOutPad, stopWhenFadeEnds, token));
        }
        catch (OperationCanceledException)
        {
            // Outro fade assumiu o controle.
        }
    }

    private void CompleteFade(AudioDeck? fadeInDeck, AudioDeck? fadeOutDeck, PadCard? fadeOutPad, bool stopWhenFadeEnds, CancellationToken token)
    {
        if (token.IsCancellationRequested)
        {
            return;
        }

        if (_fadeCancellation is not null && _fadeCancellation.Token == token)
        {
            _fadeCancellation.Dispose();
            _fadeCancellation = null;
        }

        _fadeInDeck = null;
        _fadeOutDeck = null;
        _fadeOutPad = null;
        _stopWhenFadeEnds = false;

        if (fadeOutDeck is not null)
        {
            _suppressPlaybackStopped = true;
            fadeOutDeck.Close();
            _suppressPlaybackStopped = false;
        }

        if (fadeOutPad is not null && fadeOutPad != _currentPad)
        {
            fadeOutPad.IsPlaying = false;
        }

        if (stopWhenFadeEnds)
        {
            StopPlaybackImmediate();
            return;
        }

        if (fadeInDeck is not null)
        {
            ApplyEffectiveVolume();
        }

        StatusText.Text = "Tocando - Fundo Ativo";
    }

    private void CancelPendingFadeOut()
    {
        CancelActiveFade();

        if (_fadeOutDeck is not null && !ReferenceEquals(_fadeOutDeck, _activeDeck))
        {
            _suppressPlaybackStopped = true;
            _fadeOutDeck.Close();
            _suppressPlaybackStopped = false;
        }

        if (_fadeOutPad is not null && _fadeOutPad != _currentPad)
        {
            _fadeOutPad.IsPlaying = false;
        }

        _fadeInDeck = null;
        _fadeOutDeck = null;
        _fadeOutPad = null;
        _stopWhenFadeEnds = false;
    }

    private void CancelActiveFade()
    {
        if (_fadeCancellation is null)
        {
            return;
        }

        _fadeCancellation.Cancel();
        _fadeCancellation.Dispose();
        _fadeCancellation = null;
    }

    private void PauseResume_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPad is null)
        {
            return;
        }

        if (_isPaused)
        {
            _activeDeck.Play();
            _timer.Start();
            _isPaused = false;
            StatusText.Text = "Tocando - Fundo Ativo";
        }
        else
        {
            _activeDeck.Pause();
            _timer.Stop();
            _isPaused = true;
            StatusText.Text = "Pausado - Fundo Retido";
        }
    }

    private void MasterVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MasterVolumeText is null)
        {
            return;
        }

        MasterVolumeText.Text = $"{(int)Math.Round(e.NewValue)}%";
        ApplyEffectiveVolume();
    }

    private void ApplyEffectiveVolume()
    {
        if (_activeDeck is null)
        {
            return;
        }

        if (_fadeInDeck is not null)
        {
            return;
        }

        _activeDeck.Volume = GetEffectiveVolume(_currentPad);
    }

    private double GetEffectiveVolume(PadCard? pad)
    {
        double master = MasterVolumeSlider?.Value / 100.0 ?? 0.8;
        double card = pad?.Volume / 100.0 ?? 1.0;
        return Math.Clamp(master * card, 0, 1);
    }

    private void OpenPadEditor_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if ((sender as FrameworkElement)?.Tag is not PadCard pad)
        {
            return;
        }

        _editingPad = pad;
        _editorColor = pad.Color;
        _editorSoundPath = pad.SoundPath;
        _editorCoverPath = pad.CoverPath;
        _editorLoop = pad.Loop;

        EditorTitleText.Text = $"Configurar Botao {pad.Id}";
        EditorNameBox.Text = pad.Name;
        EditorSoundText.Text = pad.SoundPath ?? "Nenhum arquivo selecionado";
        UpdateEditorCoverPreview();
        EditorVolumeSlider.Value = pad.Volume;
        EditorLoopButton.Content = pad.Loop ? "LOOP CONTINUO ATIVO" : "LOOP CONTINUO";
        ShowModal(EditorOverlay);
    }

    private void CloseEditor_Click(object sender, RoutedEventArgs e)
    {
        HideModals();
    }

    private void ColorSwatch_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is string color)
        {
            _editorColor = color;
        }
    }

    private void SelectEditorSound_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar audio",
            Filter = "Arquivos de audio|*.mp3;*.wav;*.wma;*.aac;*.ogg|Todos os arquivos|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _editorSoundPath = dialog.FileName;
            EditorSoundText.Text = dialog.FileName;
        }
    }

    private void SelectEditorCover_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar capa",
            Filter = "Imagens|*.jpg;*.jpeg;*.png;*.webp|Todos os arquivos|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            try
            {
                CoverImageService.ValidateSourceFile(dialog.FileName);
                var cropWindow = new CoverCropWindow(dialog.FileName, LocalCoversDirectory)
                {
                    Owner = this
                };

                if (cropWindow.ShowDialog() == true && !string.IsNullOrWhiteSpace(cropWindow.SavedCoverPath))
                {
                    _editorCoverPath = cropWindow.SavedCoverPath;
                    UpdateEditorCoverPreview();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Selecionar capa", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void RemoveEditorCover_Click(object sender, RoutedEventArgs e)
    {
        _editorCoverPath = null;
        UpdateEditorCoverPreview();
    }

    private void UpdateEditorCoverPreview()
    {
        if (EditorCoverImage is null)
        {
            return;
        }

        _editorCoverPath = CoverImageService.NormalizeStoredCoverPath(_editorCoverPath);
        EditorCoverImage.Source = CoverImageService.LoadCoverImage(_editorCoverPath);
        EditorCoverText.Text = CoverImageService.GetCoverLabel(_editorCoverPath);
    }

    private void EditorVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (EditorVolumeText is not null)
        {
            EditorVolumeText.Text = $"{(int)Math.Round(e.NewValue)}%";
        }
    }

    private static string FormatDb(double value)
    {
        int rounded = (int)Math.Round(value);
        return rounded > 0 ? $"+{rounded} dB" : $"{rounded} dB";
    }

    private void OpenEqualizer_Click(object sender, RoutedEventArgs e)
    {
        LoadEqualizerControls();
        ShowModal(EqualizerOverlay);
        SetEqualizerButtonHighlighted(true);
    }

    private void CloseEqualizer_Click(object sender, RoutedEventArgs e)
    {
        SaveLocalSettings();
        HideModals();
    }

    private void ResetEqualizer_Click(object sender, RoutedEventArgs e)
    {
        ApplyEqualizerPreset("padrao");
        EqEnabledCheck.IsChecked = false;
        ApplyEqualizerFromControls();
        SaveLocalSettings();
    }

    private void ApplyEqualizer_Click(object sender, RoutedEventArgs e)
    {
        ApplyEqualizerFromControls();
        SaveLocalSettings();
    }

    private void EqPreset_Click(object sender, RoutedEventArgs e)
    {
        string preset = (sender as Button)?.Tag?.ToString() ?? "padrao";
        ApplyEqualizerPreset(preset);
        ApplyEqualizerFromControls();
        SaveLocalSettings();
    }

    private void EqEnabledCheck_Changed(object sender, RoutedEventArgs e)
    {
        if (_loadingEqualizer)
        {
            return;
        }

        ApplyEqualizerFromControls();
        SaveLocalSettings();
    }

    private void EqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_loadingEqualizer)
        {
            return;
        }

        UpdateEqualizerLabels();
        ApplyEqualizerFromControls();
    }

    private void LoadEqualizerControls()
    {
        _loadingEqualizer = true;
        EqEnabledCheck.IsChecked = _eqEnabled;
        EqBassSlider.Value = _eqBass;
        EqMidSlider.Value = _eqMid;
        EqTrebleSlider.Value = _eqTreble;
        _loadingEqualizer = false;
        UpdateEqualizerLabels();
    }

    private void ApplyEqualizerPreset(string preset)
    {
        _eqPreset = preset;
        (double bass, double mid, double treble) = preset switch
        {
            "mais_grave" => (5, 0, 1),
            "voz_clara" => (-2, 3, 4),
            "suave" => (1, -1, -2),
            "ambiente" => (3, -2, 2),
            _ => (0, 0, 0)
        };

        _loadingEqualizer = true;
        EqBassSlider.Value = bass;
        EqMidSlider.Value = mid;
        EqTrebleSlider.Value = treble;
        _loadingEqualizer = false;
        UpdateEqualizerLabels();
    }

    private void ApplyEqualizerFromControls()
    {
        _eqEnabled = EqEnabledCheck.IsChecked == true;
        _eqBass = Math.Clamp(Math.Round(EqBassSlider.Value), -12, 12);
        _eqMid = Math.Clamp(Math.Round(EqMidSlider.Value), -12, 12);
        _eqTreble = Math.Clamp(Math.Round(EqTrebleSlider.Value), -12, 12);
        UpdateEqualizerLabels();
        ApplyEqualizerToDecks();
    }

    private void ApplyEqualizerToDecks()
    {
        _deckA.UpdateEqualizer(_eqEnabled, _eqBass, _eqMid, _eqTreble);
        _deckB.UpdateEqualizer(_eqEnabled, _eqBass, _eqMid, _eqTreble);
        _previewDeck.UpdateEqualizer(_eqEnabled, _eqBass, _eqMid, _eqTreble);
    }

    private void UpdateEqualizerLabels()
    {
        if (EqBassText is null)
        {
            return;
        }

        EqBassText.Text = FormatDb(EqBassSlider.Value);
        EqMidText.Text = FormatDb(EqMidSlider.Value);
        EqTrebleText.Text = FormatDb(EqTrebleSlider.Value);
    }

    private void SetEqualizerButtonHighlighted(bool highlighted)
    {
        if (EqualizerButton is null)
        {
            return;
        }

        EqualizerButton.Background = highlighted
            ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
            : new SolidColorBrush(Color.FromRgb(29, 36, 48));
        EqualizerButton.BorderBrush = highlighted
            ? new SolidColorBrush(Color.FromRgb(59, 130, 246))
            : new SolidColorBrush(Color.FromRgb(47, 58, 77));
    }

    private void SetRemoteButtonHighlighted(bool highlighted)
    {
        if (RemoteButton is null)
        {
            return;
        }

        RemoteButton.Background = highlighted
            ? new SolidColorBrush(Color.FromRgb(37, 99, 235))
            : new SolidColorBrush(Color.FromRgb(29, 36, 48));
        RemoteButton.BorderBrush = highlighted
            ? new SolidColorBrush(Color.FromRgb(59, 130, 246))
            : new SolidColorBrush(Color.FromRgb(47, 58, 77));
    }

    private void EditorLoop_Click(object sender, RoutedEventArgs e)
    {
        _editorLoop = !_editorLoop;
        EditorLoopButton.Content = _editorLoop ? "LOOP CONTINUO ATIVO" : "LOOP CONTINUO";
    }

    private void ApplyEditor_Click(object sender, RoutedEventArgs e)
    {
        if (_editingPad is null)
        {
            HideModals();
            return;
        }

        CreateAutomaticBackup("alterar-card");
        string? previousSoundPath = _editingPad.SoundPath;
        string? savedSoundPath = SaveFileInsideApp(_editorSoundPath, LocalSoundsDirectory);
        _editingPad.Name = string.IsNullOrWhiteSpace(EditorNameBox.Text) ? $"Botao {_editingPad.Id}" : EditorNameBox.Text.Trim();
        _editingPad.Color = _editorColor;
        _editingPad.SoundPath = savedSoundPath;
        _editingPad.CoverPath = CoverImageService.NormalizeStoredCoverPath(_editorCoverPath);
        _editingPad.Volume = Math.Round(EditorVolumeSlider.Value);
        _editingPad.Loop = _editorLoop;
        if (!string.Equals(previousSoundPath, savedSoundPath, StringComparison.OrdinalIgnoreCase) && IsRealAudioPath(savedSoundPath))
        {
            _editingPad.OriginalSoundPath = savedSoundPath;
        }
        else
        {
            _editingPad.OriginalSoundPath ??= IsRealAudioPath(savedSoundPath) ? savedSoundPath : null;
        }

        SaveLocalSettings();
        HideModals();
    }

    private void OpenAi_Click(object sender, RoutedEventArgs e)
    {
        _returnToEditorAfterAi = true;
        if (_editingPad is null)
        {
            return;
        }

        string? audioPath = GetEditorAudioPath();
        if (!IsRealAudioPath(audioPath) || !File.Exists(audioPath))
        {
            MessageBox.Show("Selecione um arquivo de audio antes de usar a IA.", "Remover Vocal / IA", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!string.Equals(audioPath, _editingPad.SoundPath, StringComparison.OrdinalIgnoreCase))
        {
            _editingPad.SoundPath = SaveFileInsideApp(audioPath, LocalSoundsDirectory);
            _editingPad.OriginalSoundPath = _editingPad.SoundPath;
            _editorSoundPath = _editingPad.SoundPath;
            EditorSoundText.Text = _editingPad.SoundPath ?? "Nenhum arquivo selecionado";
            SaveLocalSettings();
        }

        RefreshAiPanel();
        ShowModal(AiOverlay);
    }

    private void OpenPadAi_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if ((sender as FrameworkElement)?.Tag is not PadCard pad)
        {
            return;
        }

        if (!IsRealAudioPath(pad.SoundPath) || !File.Exists(pad.SoundPath))
        {
            MessageBox.Show("Configure um arquivo de audio neste botao antes de usar a IA.", "Remover Vocal / IA", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _editingPad = pad;
        _editorSoundPath = pad.SoundPath;
        _returnToEditorAfterAi = false;
        pad.OriginalSoundPath ??= pad.SoundPath;
        RefreshAiPanel();
        ShowModal(AiOverlay);
    }

    private void CloseAi_Click(object sender, RoutedEventArgs e)
    {
        _previewDeck.Close();
        if (_aiCancellation is not null)
        {
            CancelAi_Click(sender, e);
        }

        if (_returnToEditorAfterAi && _editingPad is not null)
        {
            ShowModal(EditorOverlay);
            return;
        }

        _returnToEditorAfterAi = false;
        HideModals();
    }

    private async void StartAi_Click(object sender, RoutedEventArgs e)
    {
        if (_editingPad is null)
        {
            return;
        }

        string mode = AiVocalOption.IsChecked == true
            ? "vocal"
            : AiOriginalOption.IsChecked == true
                ? "original"
                : "instrumental";

        if (mode == "original")
        {
            UseAudioVersion(_editingPad.OriginalSoundPath ?? _editingPad.SoundPath, isOriginal: true);
            AiStatusText.Text = "Audio original restaurado no botao.";
            return;
        }

        if (mode == "instrumental" && File.Exists(_editingPad.InstrumentalPath))
        {
            AiStatusText.Text = "Instrumental ja existia no cache local.";
            RefreshAiPanel();
            return;
        }

        if (mode == "vocal" && File.Exists(_editingPad.VocalPath))
        {
            AiStatusText.Text = "Vocal isolado ja existia no cache local.";
            RefreshAiPanel();
            return;
        }

        DemucsCommand? demucs = FindDemucsCommand();
        if (demucs is null)
        {
            AiStatusText.Text = "Recurso de IA nao instalado. Instale o pacote de separacao de audio nas configuracoes.";
            MessageBox.Show("Recurso de IA nao instalado. Instale o pacote de separacao de audio nas configuracoes.", "IA nao instalada", MessageBoxButton.OK, MessageBoxImage.Information);
            OpenAiInstallHelp_Click(sender, e);
            return;
        }

        string? originalPath = _editingPad.OriginalSoundPath ?? _editingPad.SoundPath;
        if (!IsRealAudioPath(originalPath) || !File.Exists(originalPath))
        {
            MessageBox.Show("Arquivo original nao encontrado. Selecione o audio novamente.", "IA", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Directory.CreateDirectory(LocalAiDirectory);
        string tempDirectory = Path.Combine(LocalAiDirectory, "temp_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDirectory);

        _aiCancellation = new CancellationTokenSource();
        AiStartButton.IsEnabled = false;
        AiCancelButton.IsEnabled = true;
        AiProgressBar.IsIndeterminate = false;
        AiProgressBar.Value = 0;
        AiProgressPercentText.Text = "0%";
        AiStatusText.Text = "Preparando processamento rapido com Demucs...";

        try
        {
            var progress = new Progress<DemucsProgress>(update =>
            {
                AiProgressBar.IsIndeterminate = false;
                AiProgressBar.Value = update.Percent;
                AiProgressPercentText.Text = $"{update.Percent}%";
                AiStatusText.Text = update.Message;
            });
            string demucsInputPath = PrepareDemucsInputAudio(originalPath, tempDirectory);
            await RunDemucsAsync(demucs, demucsInputPath, tempDirectory, progress, _aiCancellation.Token);
            string? instrumentalOutput = FindDemucsOutput(tempDirectory, "no_vocals");
            string? vocalOutput = FindDemucsOutput(tempDirectory, "vocals");
            if (instrumentalOutput is null || vocalOutput is null)
            {
                throw new InvalidOperationException("O Demucs terminou, mas os arquivos vocals/no_vocals nao foram encontrados.");
            }

            string baseName = MakeSafeName(Path.GetFileNameWithoutExtension(originalPath));
            string hash = CreateShortHash(originalPath);
            string outputExtension = Path.GetExtension(instrumentalOutput);
            string instrumentalPath = Path.Combine(LocalAiDirectory, $"{baseName}_{hash}_instrumental{outputExtension}");
            string vocalPath = Path.Combine(LocalAiDirectory, $"{baseName}_{hash}_vocal{Path.GetExtension(vocalOutput)}");
            File.Copy(instrumentalOutput, instrumentalPath, overwrite: true);
            File.Copy(vocalOutput, vocalPath, overwrite: true);

            _editingPad.InstrumentalPath = instrumentalPath;
            _editingPad.VocalPath = vocalPath;
            SaveLocalSettings();

            AiProgressBar.IsIndeterminate = false;
            AiProgressBar.Value = 100;
            AiProgressPercentText.Text = "100%";
            AiStatusText.Text = "Processamento concluido. Arquivos salvos localmente.";
            RefreshAiPanel();
        }
        catch (OperationCanceledException)
        {
            AiProgressBar.IsIndeterminate = false;
            AiProgressBar.Value = 0;
            AiProgressPercentText.Text = "0%";
            AiStatusText.Text = "Processamento cancelado.";
        }
        catch (Exception ex)
        {
            AiProgressBar.IsIndeterminate = false;
            AiProgressBar.Value = 0;
            AiProgressPercentText.Text = "0%";
            AiStatusText.Text = $"Nao foi possivel processar o audio. {CreateShortError(ex.Message)}";
            ShowCopyableErrorDialog("Nao foi possivel processar o audio.", ex.Message);
        }
        finally
        {
            _aiCancellation?.Dispose();
            _aiCancellation = null;
            AiStartButton.IsEnabled = true;
            AiCancelButton.IsEnabled = false;
            TryDeleteDirectory(tempDirectory);
        }
    }

    private void CancelAi_Click(object sender, RoutedEventArgs e)
    {
        _aiCancellation?.Cancel();
        AiStatusText.Text = "Cancelando processamento...";
    }

    private void ShowCopyableErrorDialog(string title, string errorText)
    {
        var dialog = new Window
        {
            Title = "IA - Erro",
            Owner = this,
            Width = 860,
            Height = 560,
            MinWidth = 620,
            MinHeight = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(21, 26, 35))
        };

        var textBox = new TextBox
        {
            Text = $"{title}{Environment.NewLine}{Environment.NewLine}{errorText}",
            IsReadOnly = true,
            TextWrapping = TextWrapping.Wrap,
            AcceptsReturn = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Background = new SolidColorBrush(Color.FromRgb(13, 18, 27)),
            Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(38, 48, 65)),
            Padding = new Thickness(10)
        };

        var copyButton = new Button
        {
            Content = "Copiar erro",
            Style = (Style)FindResource("PrimaryButton"),
            Width = 120,
            Margin = new Thickness(0, 0, 10, 0)
        };
        copyButton.Click += (_, _) =>
        {
            Clipboard.SetText(textBox.Text);
            copyButton.Content = "Copiado";
        };

        var closeButton = new Button
        {
            Content = "Fechar",
            Style = (Style)FindResource("RoundedButton"),
            Width = 100
        };
        closeButton.Click += (_, _) => dialog.Close();

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        footer.Children.Add(copyButton);
        footer.Children.Add(closeButton);

        var panel = new DockPanel { Margin = new Thickness(18) };
        DockPanel.SetDock(footer, Dock.Bottom);
        panel.Children.Add(footer);
        panel.Children.Add(textBox);
        dialog.Content = panel;
        dialog.ShowDialog();
    }

    private static string CreateShortError(string errorText)
    {
        string firstLine = errorText
            .Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault() ?? "Erro desconhecido.";
        return firstLine.Length > 160 ? firstLine[..160] + "..." : firstLine;
    }

    private void OpenAiInstallHelp_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppDataDirectory);
        string helpPath = Path.Combine(AppDataDirectory, "como_instalar_ia_local.txt");
        File.WriteAllText(helpPath, CreateAiInstallHelpText(), Encoding.UTF8);
        Process.Start(new ProcessStartInfo { FileName = "notepad.exe", Arguments = $"\"{helpPath}\"", UseShellExecute = true });
    }

    private void PreviewOriginal_Click(object sender, RoutedEventArgs e) => PreviewAudio(_editingPad?.OriginalSoundPath ?? _editingPad?.SoundPath);

    private void PreviewInstrumental_Click(object sender, RoutedEventArgs e) => PreviewAudio(_editingPad?.InstrumentalPath);

    private void PreviewVocal_Click(object sender, RoutedEventArgs e) => PreviewAudio(_editingPad?.VocalPath);

    private void UseOriginal_Click(object sender, RoutedEventArgs e) => UseAudioVersion(_editingPad?.OriginalSoundPath ?? _editingPad?.SoundPath, isOriginal: true);

    private void UseInstrumental_Click(object sender, RoutedEventArgs e) => UseAudioVersion(_editingPad?.InstrumentalPath, isOriginal: false);

    private void UseVocal_Click(object sender, RoutedEventArgs e) => UseAudioVersion(_editingPad?.VocalPath, isOriginal: false);

    private void OpenOriginalFolder_Click(object sender, RoutedEventArgs e) => OpenContainingFolder(_editingPad?.OriginalSoundPath ?? _editingPad?.SoundPath);

    private void OpenInstrumentalFolder_Click(object sender, RoutedEventArgs e) => OpenContainingFolder(_editingPad?.InstrumentalPath);

    private void OpenVocalFolder_Click(object sender, RoutedEventArgs e) => OpenContainingFolder(_editingPad?.VocalPath);

    private void RemoveInstrumental_Click(object sender, RoutedEventArgs e)
    {
        RemoveGeneratedAudio(_editingPad?.InstrumentalPath);
        if (_editingPad is not null)
        {
            _editingPad.InstrumentalPath = null;
            SaveLocalSettings();
            RefreshAiPanel();
        }
    }

    private void RemoveVocal_Click(object sender, RoutedEventArgs e)
    {
        RemoveGeneratedAudio(_editingPad?.VocalPath);
        if (_editingPad is not null)
        {
            _editingPad.VocalPath = null;
            SaveLocalSettings();
            RefreshAiPanel();
        }
    }

    private string? GetEditorAudioPath()
    {
        return IsRealAudioPath(_editorSoundPath) ? _editorSoundPath : _editingPad?.SoundPath;
    }

    private void RefreshAiPanel()
    {
        if (_editingPad is null)
        {
            return;
        }

        string? original = _editingPad.OriginalSoundPath ?? _editingPad.SoundPath;
        AiSongNameText.Text = IsRealAudioPath(original) ? Path.GetFileName(original) : "-";
        AiDurationText.Text = GetAudioDurationLabel(original);
        AiPadText.Text = $"Botao {_editingPad.Id} - {_editingPad.Name}";
        AiOriginalPathText.Text = IsRealAudioPath(original) ? original : "-";
        AiInstrumentalPathText.Text = File.Exists(_editingPad.InstrumentalPath) ? _editingPad.InstrumentalPath : "Nao gerado";
        AiVocalPathText.Text = File.Exists(_editingPad.VocalPath) ? _editingPad.VocalPath : "Nao gerado";

        bool hasInstrumental = File.Exists(_editingPad.InstrumentalPath);
        bool hasVocal = File.Exists(_editingPad.VocalPath);
        PreviewInstrumentalButton.IsEnabled = hasInstrumental;
        UseInstrumentalButton.IsEnabled = hasInstrumental;
        OpenInstrumentalFolderButton.IsEnabled = hasInstrumental;
        RemoveInstrumentalButton.IsEnabled = hasInstrumental;
        PreviewVocalButton.IsEnabled = hasVocal;
        UseVocalButton.IsEnabled = hasVocal;
        OpenVocalFolderButton.IsEnabled = hasVocal;
        RemoveVocalButton.IsEnabled = hasVocal;
    }

    private static string GetAudioDurationLabel(string? path)
    {
        if (!IsRealAudioPath(path) || !File.Exists(path))
        {
            return "-";
        }

        try
        {
            using var reader = new AudioFileReader(path);
            return FormatTime(reader.TotalTime);
        }
        catch
        {
            return "-";
        }
    }

    private void UseAudioVersion(string? path, bool isOriginal)
    {
        if (_editingPad is null || !IsRealAudioPath(path) || !File.Exists(path))
        {
            return;
        }

        PadCard? targetPad = AskPlaylistAndPad();
        if (targetPad is null)
        {
            return;
        }

        CreateAutomaticBackup("alterar-audio-card");
        targetPad.SoundPath = path;
        if (isOriginal)
        {
            targetPad.OriginalSoundPath = path;
        }
        else if (path.Equals(_editingPad.InstrumentalPath, StringComparison.OrdinalIgnoreCase))
        {
            targetPad.InstrumentalPath = path;
            targetPad.OriginalSoundPath ??= _editingPad.OriginalSoundPath ?? _editingPad.SoundPath;
        }
        else if (path.Equals(_editingPad.VocalPath, StringComparison.OrdinalIgnoreCase))
        {
            targetPad.VocalPath = path;
            targetPad.OriginalSoundPath ??= _editingPad.OriginalSoundPath ?? _editingPad.SoundPath;
        }

        if (targetPad == _editingPad)
        {
            _editorSoundPath = path;
            EditorSoundText.Text = path;
        }

        SaveLocalSettings();
        RefreshAiPanel();
        AiStatusText.Text = $"Audio aplicado em {PlaylistNameText.Text}, Botao {targetPad.Id}.";
    }

    private void PreviewAudio(string? path)
    {
        if (!IsRealAudioPath(path) || !File.Exists(path))
        {
            MessageBox.Show("Arquivo de audio nao encontrado.", "Preview", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            _previewDeck.Close();
            _previewDeck.Load(path, _eqEnabled, _eqBass, _eqMid, _eqTreble);
            _previewDeck.Volume = GetEffectiveVolume(_editingPad);
            _previewDeck.Play();
            AiStatusText.Text = $"Preview: {Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Nao foi possivel tocar o preview.\n{ex.Message}", "Preview", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void OpenContainingFolder(string? path)
    {
        if (!IsRealAudioPath(path) || !File.Exists(path))
        {
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Process.Start(new ProcessStartInfo { FileName = directory, UseShellExecute = true });
        }
    }

    private static void RemoveGeneratedAudio(string? path)
    {
        if (!IsRealAudioPath(path) || !File.Exists(path))
        {
            return;
        }

        string fullPath = Path.GetFullPath(path);
        string aiDirectory = Path.GetFullPath(LocalAiDirectory);
        if (!fullPath.StartsWith(aiDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        File.Delete(fullPath);
    }

    private static string PrepareDemucsInputAudio(string originalPath, string tempDirectory)
    {
        string extension = Path.GetExtension(originalPath);
        if (string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase))
        {
            return originalPath;
        }

        Directory.CreateDirectory(tempDirectory);
        string wavPath = Path.Combine(tempDirectory, $"{MakeSafeName(Path.GetFileNameWithoutExtension(originalPath))}_demucs_input.wav");
        using var reader = new AudioFileReader(originalPath);
        WaveFileWriter.CreateWaveFile16(wavPath, reader);
        return wavPath;
    }

    private static string? FindDemucsOutput(string tempDirectory, string stem)
    {
        string[] extensions = [".mp3", ".wav", ".flac"];
        foreach (string extension in extensions)
        {
            string? path = Directory
                .GetFiles(tempDirectory, $"{stem}{extension}", SearchOption.AllDirectories)
                .FirstOrDefault();
            if (path is not null)
            {
                return path;
            }
        }

        return null;
    }

    private static async Task RunDemucsAsync(DemucsCommand demucs, string audioPath, string outputDirectory, IProgress<DemucsProgress> progress, CancellationToken cancellationToken)
    {
        string arguments = $"{demucs.ArgumentsPrefix} --two-stems=vocals --shifts=1 --overlap=0.1 --mp3 --mp3-bitrate=192 --mp3-preset=7 -o \"{outputDirectory}\" \"{audioPath}\"".Trim();
        bool isBatchFile = demucs.FileName.EndsWith(".cmd", StringComparison.OrdinalIgnoreCase) || demucs.FileName.EndsWith(".bat", StringComparison.OrdinalIgnoreCase);
        var startInfo = new ProcessStartInfo
        {
            FileName = isBatchFile ? "cmd.exe" : demucs.FileName,
            Arguments = isBatchFile ? $"/c \"\"{demucs.FileName}\" {arguments}\"" : arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            StandardErrorEncoding = Encoding.UTF8,
            StandardOutputEncoding = Encoding.UTF8
        };
        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.Environment["PYTHONIOENCODING"] = "utf-8";
        AddFfmpegDirectoriesToPath(startInfo);

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        progress.Report(new DemucsProgress(1, "Iniciando Demucs em modo rapido..."));
        if (!process.Start())
        {
            throw new InvalidOperationException("Nao foi possivel iniciar o Demucs.");
        }

        using CancellationTokenRegistration registration = cancellationToken.Register(() =>
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignora falhas de encerramento de processo externo.
            }
        });

        Task<string> stderrTask = ReadDemucsOutputAsync(process.StandardError, progress, cancellationToken);
        Task<string> stdoutTask = ReadDemucsOutputAsync(process.StandardOutput, progress, cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        string stderr = await stderrTask;
        string stdout = await stdoutTask;

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(CreateDemucsErrorMessage(string.IsNullOrWhiteSpace(stderr) ? stdout : stderr));
        }
    }

    private static async Task<string> ReadDemucsOutputAsync(StreamReader reader, IProgress<DemucsProgress> progress, CancellationToken cancellationToken)
    {
        var output = new StringBuilder();
        var recent = new StringBuilder();
        char[] buffer = new char[512];

        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
            if (read <= 0)
            {
                break;
            }

            string chunk = new(buffer, 0, read);
            output.Append(chunk);
            recent.Append(chunk);
            if (recent.Length > 3000)
            {
                recent.Remove(0, recent.Length - 3000);
            }

            ReportDemucsProgress(recent.ToString(), progress);
        }

        return output.ToString();
    }

    private static void ReportDemucsProgress(string text, IProgress<DemucsProgress> progress)
    {
        MatchCollection matches = Regex.Matches(text, @"(?<!\d)(\d{1,3})%\|");
        if (matches.Count == 0)
        {
            matches = Regex.Matches(text, @"(?<!\d)(\d{1,3})%");
        }

        if (matches.Count == 0)
        {
            if (text.Contains("Separating track", StringComparison.OrdinalIgnoreCase))
            {
                progress.Report(new DemucsProgress(5, "Separando vocal e instrumental..."));
            }
            else if (text.Contains("Downloading", StringComparison.OrdinalIgnoreCase))
            {
                progress.Report(new DemucsProgress(2, "Baixando modelo local do Demucs uma unica vez..."));
            }

            return;
        }

        string value = matches[^1].Groups[1].Value;
        if (!int.TryParse(value, out int percent))
        {
            return;
        }

        percent = Math.Clamp(percent, 0, 100);
        string message = percent < 100
            ? $"Processando audio localmente com Demucs... {percent}%"
            : "Finalizando arquivos gerados...";
        progress.Report(new DemucsProgress(percent, message));
    }

    private static void AddFfmpegDirectoriesToPath(ProcessStartInfo startInfo)
    {
        var directories = new List<string>();
        string? existingPath = startInfo.Environment.TryGetValue("PATH", out string? value)
            ? value
            : Environment.GetEnvironmentVariable("PATH");

        foreach (string directory in GetFfmpegDirectories())
        {
            if (!directories.Contains(directory, StringComparer.OrdinalIgnoreCase))
            {
                directories.Add(directory);
            }
        }

        if (directories.Count == 0)
        {
            return;
        }

        string prefix = string.Join(Path.PathSeparator, directories);
        startInfo.Environment["PATH"] = string.IsNullOrWhiteSpace(existingPath)
            ? prefix
            : $"{prefix}{Path.PathSeparator}{existingPath}";
    }

    private static IEnumerable<string> GetFfmpegDirectories()
    {
        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (pathVariable is not null)
        {
            foreach (string directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = directory.Trim();
                if (File.Exists(Path.Combine(trimmed, "ffmpeg.exe")))
                {
                    yield return trimmed;
                }
            }
        }

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string wingetRoot = Path.Combine(local, "Microsoft", "WinGet");
        if (Directory.Exists(wingetRoot))
        {
            foreach (string ffmpeg in Directory.EnumerateFiles(wingetRoot, "ffmpeg.exe", SearchOption.AllDirectories))
            {
                string? directory = Path.GetDirectoryName(ffmpeg);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    yield return directory;
                }
            }
        }
    }

    private static string CreateDemucsErrorMessage(string output)
    {
        if (output.Contains("FFmpeg is not installed", StringComparison.OrdinalIgnoreCase)
            || output.Contains("Could not load libtorchcodec", StringComparison.OrdinalIgnoreCase)
            || output.Contains("libtorchcodec", StringComparison.OrdinalIgnoreCase))
        {
            return $"""
            O Demucs rodou, mas o ambiente Python nao conseguiu carregar o audio.

            O app ja tentou converter o arquivo para WAV antes de processar. Se ainda aparecer este erro, instale as dependencias de leitura de audio:

            python -m pip install --upgrade pip setuptools wheel
            python -m pip install -U torchcodec torchaudio

            Se continuar, instale o FFmpeg para Windows e reinicie o app:
            winget install Gyan.FFmpeg

            Erro original:
            {output}
            """;
        }

        if (output.Contains("No module named 'torchcodec'", StringComparison.OrdinalIgnoreCase)
            || output.Contains("No module named \"torchcodec\"", StringComparison.OrdinalIgnoreCase))
        {
            return """
            O Demucs esta instalado, mas falta o pacote de leitura de audio torchcodec.

            Abra o Prompt de Comando e execute estes comandos:
            python -m pip install --upgrade pip
            python -m pip install -U torchcodec

            Se continuar dando erro, abra "Como instalar IA local" para ver o passo a passo completo.
            Depois feche e abra novamente o Som de Fundo Pro.
            """;
        }

        if (output.Contains("UnicodeEncodeError", StringComparison.OrdinalIgnoreCase))
        {
            return """
            O Demucs encontrou um erro de texto/acentuacao ao processar este arquivo.

            O app agora executa o Demucs em UTF-8. Feche e abra novamente o Som de Fundo Pro e tente de novo.
            Se continuar, renomeie o arquivo para um nome simples, sem acentos ou simbolos, e selecione-o novamente.
            """;
        }

        return output;
    }

    private static string CreateAiInstallHelpText()
    {
        return """
        COMO INSTALAR A IA LOCAL DO SOM DE FUNDO PRO
        ============================================

        O que este recurso faz:
        - Usa Demucs para separar vocal e instrumental.
        - Todo o processamento acontece no computador.
        - Nenhuma musica e enviada para servidor.
        - O arquivo original nunca e apagado nem modificado.

        PASSO 1 - Instalar Python
        -------------------------
        1. Baixe o Python no site oficial:
           https://www.python.org/downloads/

        2. Recomendado para usuarios leigos:
           Python 3.10 ou Python 3.11.

        3. Na tela de instalacao, marque:
           Add python.exe to PATH

        4. Depois clique em Install Now.

        Observacao:
        Se voce ja tem Python instalado e ele funciona, pode continuar.

        PASSO 2 - Abrir o Prompt de Comando
        -----------------------------------
        1. Aperte Windows + R.
        2. Digite:
           cmd
        3. Aperte Enter.

        PASSO 3 - Atualizar o instalador de pacotes
        -------------------------------------------
        Copie e cole este comando no Prompt:

        python -m pip install --upgrade pip setuptools wheel

        PASSO 4 - Instalar o Demucs
        ---------------------------
        Copie e cole este comando:

        python -m pip install -U demucs

        PASSO 5 - Instalar dependencias de leitura de audio
        ---------------------------------------------------
        Copie e cole este comando:

        python -m pip install -U torchcodec

        Se ainda der erro para ler MP3, instale tambem:

        python -m pip install -U torchaudio

        PASSO 5B - Instalar FFmpeg se o erro continuar
        ----------------------------------------------
        Se aparecer "FFmpeg is not installed", execute:

        winget install Gyan.FFmpeg

        Depois feche e abra novamente o Prompt de Comando e teste:

        ffmpeg -version

        PASSO 6 - Testar se o Demucs funciona
        -------------------------------------
        Primeiro teste:

        demucs --help

        Se nao funcionar, teste:

        python -m demucs --help

        Se aparecer uma lista grande de opcoes, a IA local esta instalada.
        Nao tem problema se "demucs --help" nao funcionar por causa do PATH.
        O Som de Fundo Pro tambem procura a pasta Scripts do Python e usa "python -m demucs".

        PASSO 7 - Reiniciar o app
        -------------------------
        Feche completamente o Som de Fundo Pro.
        Abra novamente o app.
        Tente processar o audio outra vez.

        ERROS COMUNS
        ============

        1. "Recurso de IA nao instalado"
           Execute:
           python -m pip install -U demucs

        2. "No module named torchcodec"
           Execute:
           python -m pip install -U torchcodec

        3. "demucs nao e reconhecido"
           Use o teste alternativo:
           python -m demucs --help
           Se esse comando funcionar, pode usar o app normalmente.

        4. Erro com acentos ou simbolos no nome do arquivo
           Renomeie a musica para um nome simples, por exemplo:
           musica_teste.mp3

        5. Computador lento ou processamento demorando
           Isso e normal. Separacao de vocal usa bastante processador.

        6. Se nada funcionar
           Execute estes comandos em ordem:
           python -m pip install --upgrade pip setuptools wheel
           python -m pip install -U demucs
           python -m pip install -U torchcodec torchaudio
           winget install Gyan.FFmpeg
           python -m demucs --help

        Referencia oficial do Demucs:
        https://github.com/facebookresearch/demucs
        """;
    }

    private static DemucsCommand? FindDemucsCommand()
    {
        string[] names = ["demucs.exe", "demucs.cmd", "demucs.bat"];
        string? pathVariable = Environment.GetEnvironmentVariable("PATH");
        if (pathVariable is not null)
        {
            foreach (string directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            {
                foreach (string name in names)
                {
                    string candidate = Path.Combine(directory.Trim(), name);
                    if (File.Exists(candidate))
                    {
                        return new DemucsCommand(candidate, "");
                    }
                }
            }
        }

        foreach (string directory in GetPythonScriptsDirectories())
        {
            foreach (string name in names)
            {
                string candidate = Path.Combine(directory, name);
                if (File.Exists(candidate))
                {
                    return new DemucsCommand(candidate, "");
                }
            }
        }

        if (CanRunDemucsModule("python"))
        {
            return new DemucsCommand("python", "-m demucs");
        }

        if (CanRunDemucsModule("py"))
        {
            return new DemucsCommand("py", "-m demucs");
        }

        return null;
    }

    private static IEnumerable<string> GetPythonScriptsDirectories()
    {
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string pythonRoot = Path.Combine(roaming, "Python");
        if (Directory.Exists(pythonRoot))
        {
            foreach (string directory in Directory.EnumerateDirectories(pythonRoot, "Python*"))
            {
                string scripts = Path.Combine(directory, "Scripts");
                if (Directory.Exists(scripts))
                {
                    yield return scripts;
                }
            }
        }

        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string localProgramsPython = Path.Combine(local, "Programs", "Python");
        if (Directory.Exists(localProgramsPython))
        {
            foreach (string directory in Directory.EnumerateDirectories(localProgramsPython, "Python*"))
            {
                string scripts = Path.Combine(directory, "Scripts");
                if (Directory.Exists(scripts))
                {
                    yield return scripts;
                }
            }
        }
    }

    private static bool CanRunDemucsModule(string pythonCommand)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = pythonCommand,
                    Arguments = "-m demucs --help",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                }
            };

            process.Start();
            if (!process.WaitForExit(8000))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            return output.Contains("demucs", StringComparison.OrdinalIgnoreCase)
                || output.Contains("separate", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRealAudioPath(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && !path.StartsWith("offline://", StringComparison.OrdinalIgnoreCase);
    }

    private static string MakeSafeName(string name)
    {
        string safeName = string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(safeName) ? "audio" : safeName;
    }

    private static string CreateShortHash(string value)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash)[..10].ToLowerInvariant();
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // Arquivos temporarios podem ficar presos por antivirus ou pelo proprio Demucs.
        }
    }

    private void LoopPad_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if ((sender as FrameworkElement)?.Tag is PadCard pad)
        {
            pad.Loop = !pad.Loop;
            SaveLocalSettings();
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        double availableFromWindow = ActualHeight > 0 ? ActualHeight - 56 : SystemParameters.WorkArea.Height - 80;
        double availableHeight = Math.Min(SystemParameters.WorkArea.Height - 80, availableFromWindow);
        SettingsOverlay.MaxHeight = Math.Max(520, availableHeight);
        RefreshSupabaseInternalStatus();
        ShowModal(SettingsOverlay);
    }

    private void CloseSettings_Click(object sender, RoutedEventArgs e)
    {
        HideModals();
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        SaveLocalSettings();
        HideModals();
    }

    private async void TestSupabase_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(GetSupabaseUrl()) || string.IsNullOrWhiteSpace(GetSupabasePublicKey()))
        {
            MessageBox.Show("A biblioteca online ainda nao foi configurada internamente.", "Biblioteca Online", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await _communityClient.InitializeAsync();
            List<CommunityMusic> result = await _communityClient.GetMusicsAsync("", 0, adminView: false);
            bool admin = await _communityClient.IsAdminAsync();
            MessageBox.Show($"Biblioteca online conectada.\nPerfil: {(admin ? "administrador" : "usuario")}\nMusicas lidas: {result.Count}", "Biblioteca Online", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            string details = IsAdminBuild
                ? $"{BuildSupabaseConfigSummary()}{Environment.NewLine}{Environment.NewLine}{GetFriendlySupabaseError(ex)}"
                : GetFriendlySupabaseError(ex);
            ShowCopyableErrorDialog("Nao foi possivel testar a biblioteca online.", details);
        }
    }

    private void RefreshSupabaseInternalStatus()
    {
        if (SupabaseInternalStatusText is null)
        {
            return;
        }

        SupabaseInternalStatusText.Text = string.IsNullOrWhiteSpace(GetSupabaseUrl()) || string.IsNullOrWhiteSpace(GetSupabasePublicKey())
            ? "Biblioteca online aguardando configuracao interna."
            : "Biblioteca online configurada internamente.";
    }

    private static string GetSupabaseUrl()
    {
        return GetSecretValue(SupabaseUrlKey, "SUPABASE_URL");
    }

    private static string GetSupabasePublicKey()
    {
        return GetSecretValue(SupabasePublicKeyKey, SupabaseAnonKeyAlias);
    }

    private static string GetSupabaseBucket()
    {
        string bucket = GetSecretValue(SupabaseBucketKey);
        return string.IsNullOrWhiteSpace(bucket) ? "online-audios" : bucket;
    }

    private static string GetSecretValue(params string[] keys)
    {
        foreach (string key in keys)
        {
            if (DotEnvValues.Value.TryGetValue(key, out string? dotEnvValue) && !string.IsNullOrWhiteSpace(dotEnvValue))
            {
                return dotEnvValue.Trim();
            }
        }

        foreach (string key in keys)
        {
            string? value = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return "";
    }

    private static Dictionary<string, string> LoadDotEnvValues()
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in GetDotEnvPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator <= 0)
                {
                    continue;
                }

                string key = line[..separator].Trim();
                string value = line[(separator + 1)..].Trim().Trim('"');
                values[key] = value;
            }
        }

        return values;
    }

    private static IEnumerable<string> GetDotEnvPaths()
    {
        yield return Path.Combine(AppContext.BaseDirectory, ".env");
        yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
        yield return Path.Combine(Directory.GetCurrentDirectory(), ".env");
    }

    private static Dictionary<string, string> LoadDotEnvValueSources()
    {
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in GetDotEnvPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                if (separator > 0)
                {
                    sources[line[..separator].Trim()] = path;
                }
            }
        }

        return sources;
    }

    private static string GetSecretSource(params string[] keys)
    {
        foreach (string key in keys)
        {
            if (DotEnvValueSources.Value.TryGetValue(key, out string? source))
            {
                return source;
            }
        }

        foreach (string key in keys)
        {
            if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            {
                return $"variavel de ambiente {key}";
            }
        }

        return "nao configurado";
    }

    private static string MaskSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(vazio)";
        }

        string trimmed = value.Trim();
        if (trimmed.Length <= 12)
        {
            return "***";
        }

        return $"{trimmed[..8]}...{trimmed[^4..]}";
    }

    private static string BuildSupabaseConfigSummary()
    {
        string url = GetSupabaseUrl();
        string key = GetSupabasePublicKey();
        return
            $"SUPABASE_URL usada: {url}{Environment.NewLine}" +
            $"Fonte da URL: {GetSecretSource(SupabaseUrlKey, "SUPABASE_URL")}{Environment.NewLine}" +
            $"Chave publica usada: {MaskSecret(key)}{Environment.NewLine}" +
            $"Fonte da chave: {GetSecretSource(SupabasePublicKeyKey, SupabaseAnonKeyAlias)}{Environment.NewLine}" +
            $"Bucket: {GetSupabaseBucket()}";
    }

    private static void LogSupabaseStartupConfig()
    {
        try
        {
            Directory.CreateDirectory(AppDataDirectory);
            string line = $"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}] {BuildSupabaseConfigSummary().Replace(Environment.NewLine, " | ")}";
            File.AppendAllText(Path.Combine(AppDataDirectory, "supabase-debug.log"), line + Environment.NewLine);
            Debug.WriteLine(line);
        }
        catch
        {
        }
    }

    private static string GetFriendlySupabaseError(Exception ex)
    {
        string message = ex.ToString();
        if (message.Contains("anonymous_provider_disabled", StringComparison.OrdinalIgnoreCase))
        {
            return "A sessao anonima da biblioteca online esta desativada no servico configurado para este app." +
                (IsAdminBuild ? Environment.NewLine + Environment.NewLine + message : "");
        }

        return IsAdminBuild ? message : "O servico da biblioteca online nao respondeu corretamente. Tente novamente mais tarde.";
    }

    private void CopySupabaseSql_Click(object sender, RoutedEventArgs e)
    {
        Clipboard.SetText(SupabaseSchemaSql);
        MessageBox.Show("SQL da biblioteca online copiado.", "Supabase", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void UpdateSupabaseSqlBox()
    {
        if (SupabaseSqlBox is not null)
        {
            SupabaseSqlBox.Text = SupabaseSchemaSql;
        }
    }

    private void CardSize_Click(object sender, RoutedEventArgs e)
    {
        string size = (sender as Button)?.Tag?.ToString() ?? "Medium";
        _basePadHeight = size switch
        {
            "Small" => 130,
            "Large" => 170,
            _ => 150
        };
        OnPropertyChanged(nameof(PadHeight));
    }

    private void ColumnsSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ColumnsLabel is null)
        {
            return;
        }

        GridColumns = (int)Math.Round(e.NewValue);
        ColumnsLabel.Text = $"Colunas da Grade ({GridColumns} colunas)";
    }

    private void ScaleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScaleLabel is null)
        {
            return;
        }

        _scalePercent = Math.Round(e.NewValue);
        ScaleLabel.Text = $"Ajuste de Escala Visual ({_scalePercent:0}%)";
        OnPropertyChanged(nameof(PadHeight));
    }

    private void CrossfadeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CrossfadeLabel is null)
        {
            return;
        }

        _crossfadeSeconds = (int)Math.Round(e.NewValue);
        CrossfadeLabel.Text = $"Duracao do Crossfade ({_crossfadeSeconds}s)";
        CrossfadeText.Text = $"Crossfade: {_crossfadeSeconds}s";
    }

    private BackupData CreateBackupData()
    {
        if (_playlists.Count > 0)
        {
            CaptureCurrentPlaylist();
        }

        return new BackupData
        {
            Pin = PinBox.Text,
            GridColumns = GridColumns,
            ScalePercent = _scalePercent,
            CrossfadeSeconds = _crossfadeSeconds,
            EqEnabled = _eqEnabled,
            EqBass = _eqBass,
            EqMid = _eqMid,
            EqTreble = _eqTreble,
            EqPreset = _eqPreset,
            CommunitySenderName = UploadSenderBox?.Text?.Trim(),
            CurrentPlaylistIndex = _currentPlaylistIndex,
            FirstRunCompleted = _firstRunCompleted,
            Playlists = _playlists.Select(PlaylistBackup.FromState).ToList(),
            Pads = _pads.Select(PadBackup.FromPad).ToList()
        };
    }

    private void ApplyBackupData(BackupData backup)
    {
        PinBox.Text = backup.Pin ?? "1234";
        _firstRunCompleted = backup.FirstRunCompleted;
        GridColumns = Math.Clamp(backup.GridColumns, 5, 8);
        ColumnsSlider.Value = GridColumns;
        _scalePercent = Math.Clamp(backup.ScalePercent <= 0 ? 100 : backup.ScalePercent, 80, 120);
        ScaleSlider.Value = _scalePercent;
        _crossfadeSeconds = Math.Clamp(backup.CrossfadeSeconds <= 0 ? 3 : backup.CrossfadeSeconds, 1, 10);
        CrossfadeSlider.Value = _crossfadeSeconds;
        _eqEnabled = backup.EqEnabled;
        _eqBass = Math.Clamp(backup.EqBass, -12, 12);
        _eqMid = Math.Clamp(backup.EqMid, -12, 12);
        _eqTreble = Math.Clamp(backup.EqTreble, -12, 12);
        _eqPreset = string.IsNullOrWhiteSpace(backup.EqPreset) ? "padrao" : backup.EqPreset;
        if (!string.IsNullOrWhiteSpace(backup.CommunitySenderName))
        {
            UploadSenderBox.Text = backup.CommunitySenderName;
        }
        ApplyEqualizerToDecks();

        if (backup.Playlists.Count > 0)
        {
            _playlists.Clear();
            foreach (PlaylistBackup savedPlaylist in backup.Playlists)
            {
                string name = string.IsNullOrWhiteSpace(savedPlaylist.Name) ? DefaultPlaylistName : savedPlaylist.Name.Trim();
                _playlists.Add(new PlaylistState(name, savedPlaylist.Pads.Select(PlaylistBackup.ClonePad).ToList()));
            }

            _currentPlaylistIndex = Math.Clamp(backup.CurrentPlaylistIndex, 0, _playlists.Count - 1);
            ApplyCurrentPlaylist();
            return;
        }

        foreach (PadBackup saved in backup.Pads)
        {
            PadCard? pad = _pads.FirstOrDefault(p => p.Id == saved.Id);
            if (pad is not null)
            {
                saved.ApplyTo(pad);
            }
        }
    }

    private void LoadLocalSettings()
    {
        if (!File.Exists(LocalSettingsPath))
        {
            return;
        }

        try
        {
            BackupData? backup = JsonSerializer.Deserialize<BackupData>(File.ReadAllText(LocalSettingsPath));
            if (backup is not null)
            {
                ApplyBackupData(backup);
                _loadedExistingSettings = true;
            }
        }
        catch
        {
            // Config local corrompida nao deve impedir o app de abrir.
        }
    }

    private void SaveLocalSettings()
    {
        Directory.CreateDirectory(AppDataDirectory);
        File.WriteAllText(LocalSettingsPath, JsonSerializer.Serialize(CreateBackupData(), new JsonSerializerOptions { WriteIndented = true }));
        AppendAppLog("Configuracoes salvas.");
    }

    private void CreateAutomaticBackup(string reason)
    {
        try
        {
            Directory.CreateDirectory(LocalBackupsDirectory);
            string safeReason = Regex.Replace(reason, @"[^A-Za-z0-9_-]+", "-").Trim('-');
            if (string.IsNullOrWhiteSpace(safeReason))
            {
                safeReason = "alteracao";
            }

            string path = Path.Combine(LocalBackupsDirectory, $"backup-{DateTime.Now:yyyyMMdd-HHmmss}-{safeReason}.json");
            File.WriteAllText(path, JsonSerializer.Serialize(CreateBackupData(), new JsonSerializerOptions { WriteIndented = true }));
            TrimAutomaticBackups();
            AppendAppLog($"Backup automatico criado: {safeReason}");
        }
        catch (Exception ex)
        {
            AppendAppLog($"Falha ao criar backup automatico: {ex.Message}");
        }
    }

    private static void TrimAutomaticBackups()
    {
        if (!Directory.Exists(LocalBackupsDirectory))
        {
            return;
        }

        foreach (FileInfo file in new DirectoryInfo(LocalBackupsDirectory)
                     .GetFiles("backup-*.json")
                     .OrderByDescending(file => file.CreationTimeUtc)
                     .Skip(MaxAutomaticBackups))
        {
            try
            {
                file.Delete();
            }
            catch
            {
                // Backup antigo travado pode ficar para a proxima limpeza.
            }
        }
    }

    private static string? SaveFileInsideApp(string? sourcePath, string targetDirectory)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || sourcePath.StartsWith("offline://", StringComparison.OrdinalIgnoreCase))
        {
            return sourcePath;
        }

        if (!File.Exists(sourcePath))
        {
            return sourcePath;
        }

        Directory.CreateDirectory(targetDirectory);
        string fullTargetDirectory = Path.GetFullPath(targetDirectory);
        string fullSourcePath = Path.GetFullPath(sourcePath);
        if (Path.GetDirectoryName(fullSourcePath)?.Equals(fullTargetDirectory, StringComparison.OrdinalIgnoreCase) == true)
        {
            return fullSourcePath;
        }

        string extension = Path.GetExtension(sourcePath);
        string safeName = string.Join("_", Path.GetFileNameWithoutExtension(sourcePath).Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "audio";
        }

        string targetPath = Path.Combine(fullTargetDirectory, $"{safeName}_{DateTime.Now:yyyyMMddHHmmssfff}{extension}");
        File.Copy(sourcePath, targetPath, overwrite: false);
        return targetPath;
    }

    private string BuildDiagnosticsReport()
    {
        RefreshAllPadAudioStatus();
        var report = new StringBuilder();
        report.AppendLine("Som de Fundo Pro - Diagnostico");
        report.AppendLine($"Gerado em: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"Versao: {GetAppVersion()}");
        report.AppendLine();
        report.AppendLine("Controle remoto");
        report.AppendLine($"Status: {(_remoteServer is null ? "desligado" : "ligado")}");
        report.AppendLine($"Endereco: {GetRemoteBaseUrl()}");
        report.AppendLine($"Conectados: {_remoteConnections}");
        report.AppendLine($"PIN: {MaskPin(PinBox.Text)}");
        report.AppendLine();
        report.AppendLine("Configuracoes");
        report.AppendLine($"Playlist atual: {(_playlists.Count > 0 ? _playlists[_currentPlaylistIndex].Name : "-")}");
        report.AppendLine($"Playlists: {_playlists.Count}");
        report.AppendLine($"Cards: {_pads.Count}");
        report.AppendLine($"Volume geral: {MasterVolumeSlider.Value:0}%");
        report.AppendLine($"Crossfade: {_crossfadeSeconds}s");
        report.AppendLine($"Equalizador: {(_eqEnabled ? "ativo" : "desativado")} / preset {_eqPreset}");
        report.AppendLine($"Pasta de sons: {LocalSoundsDirectory}");
        report.AppendLine();
        report.AppendLine("Cards com arquivo ausente");
        foreach (PadCard pad in _pads.Where(pad => pad.HasMissingAudio))
        {
            report.AppendLine($"- [{pad.Id}] {pad.Name}: {Path.GetFileName(pad.SoundPath)}");
        }

        if (!_pads.Any(pad => pad.HasMissingAudio))
        {
            report.AppendLine("- Nenhum");
        }

        report.AppendLine();
        report.AppendLine("Logs recentes");
        foreach (string line in ReadRecentLogLines(80))
        {
            report.AppendLine(line);
        }

        return report.ToString();
    }

    private static string GetAppVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";
    }

    private static string MaskPin(string? pin)
    {
        if (string.IsNullOrWhiteSpace(pin))
        {
            return "-";
        }

        string trimmed = pin.Trim();
        return trimmed.Length <= 2 ? "**" : $"{trimmed[..1]}***{trimmed[^1..]}";
    }

    private static void AppendAppLog(string message)
    {
        try
        {
            Directory.CreateDirectory(LocalLogsDirectory);
            string path = Path.Combine(LocalLogsDirectory, "app.log");
            File.AppendAllText(path, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Log nunca deve impedir o app de funcionar.
        }
    }

    private static IEnumerable<string> ReadRecentLogLines(int maxLines)
    {
        try
        {
            string path = Path.Combine(LocalLogsDirectory, "app.log");
            return File.Exists(path)
                ? File.ReadLines(path).TakeLast(maxLines).ToList()
                : ["Sem logs recentes."];
        }
        catch
        {
            return ["Nao foi possivel ler os logs recentes."];
        }
    }

    private void ExportBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exportar backup",
            Filter = "JSON|*.json",
            FileName = "som-de-fundo-backup.json"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(CreateBackupData(), new JsonSerializerOptions { WriteIndented = true }));
        MessageBox.Show("Backup exportado.", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ImportBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Importar backup",
            Filter = "JSON|*.json|Todos os arquivos|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            CreateAutomaticBackup("importar-backup");
            BackupData? backup = JsonSerializer.Deserialize<BackupData>(File.ReadAllText(dialog.FileName));
            if (backup is null)
            {
                return;
            }

            ApplyBackupData(backup);
            SaveLocalSettings();

            MessageBox.Show("Backup importado.", "Backup", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Nao foi possivel importar o backup.\n{ex.Message}", "Backup", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OpenLibrary_Click(object sender, RoutedEventArgs e)
    {
        ShowModal(LibraryOverlay);
        UpdateLibraryPanel();
        await InitializeCommunityAsync();
        _communityClient.StartRealtime(async () => await Dispatcher.InvokeAsync(async () => await LoadSupabaseLibraryAsync()));
        await LoadSupabaseLibraryAsync();
    }

    private void CloseLibrary_Click(object sender, RoutedEventArgs e)
    {
        _communityClient.StopRealtime();
        HideModals();
    }

    private void LibraryTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (e.Source == LibraryTabs)
        {
            UpdateLibraryPanel();
        }
    }

    private void UpdateLibraryPanel()
    {
        if (TopLibraryPanel is null)
        {
            return;
        }

        TopLibraryPanel.Visibility = LibraryTabs.SelectedIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
        UploadLibraryPanel.Visibility = LibraryTabs.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        VotingLibraryPanel.Visibility = IsAdminBuild && LibraryTabs.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        AdminCleanupPanel.Visibility = IsAdminBuild && LibraryTabs.SelectedIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
        SqlLibraryPanel.Visibility = IsAdminBuild && LibraryTabs.SelectedIndex == 4 ? Visibility.Visible : Visibility.Collapsed;

        if (LibraryTabs.SelectedIndex is 2 or 3 && (!IsAdminBuild || !_isCommunityAdmin))
        {
            LibraryStatusText.Text = IsAdminBuild
                ? "Status: area administrativa bloqueada. Cadastre seu User ID em admin_users."
                : "Status: app de usuario sem area administrativa.";
        }

        if (IsAdminBuild && LibraryTabs.SelectedIndex == 3 && _isCommunityAdmin)
        {
            _ = RunAdminCleanupAsync(autoRun: true);
        }
    }

    private async Task InitializeCommunityAsync()
    {
        if (string.IsNullOrWhiteSpace(GetSupabaseUrl()) || string.IsNullOrWhiteSpace(GetSupabasePublicKey()))
        {
            LibraryStatusText.Text = "Status: biblioteca online indisponivel";
            return;
        }

        try
        {
            await _communityClient.InitializeAsync();
            _isCommunityAdmin = IsAdminBuild && await _communityClient.IsAdminAsync();
            CommunityUserIdText.Text = $"User ID: {_communityClient.UserId}";
            AdminStatusText.Text = _isCommunityAdmin
                ? "Area administrativa liberada."
                : IsAdminBuild ? "Area administrativa bloqueada. Cadastre este User ID na tabela admin_users." : "App de usuario.";
            CleanupStatusText.Text = _isCommunityAdmin
                ? "Limpeza pronta para rodar automaticamente."
                : IsAdminBuild ? "Sem permissao de admin para limpeza." : "Limpeza indisponivel no app de usuario.";
        }
        catch (Exception ex)
        {
            LibraryStatusText.Text = "Status: erro ao conectar biblioteca online";
            string details = IsAdminBuild
                ? $"{BuildSupabaseConfigSummary()}{Environment.NewLine}{Environment.NewLine}{GetFriendlySupabaseError(ex)}"
                : GetFriendlySupabaseError(ex);
            ShowCopyableErrorDialog("Nao foi possivel conectar a biblioteca online.", details);
        }
    }

    private async Task LoadSupabaseLibraryAsync()
    {
        if (_loadingLibrary)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(GetSupabaseUrl()) || string.IsNullOrWhiteSpace(GetSupabasePublicKey()))
        {
            LibraryStatusText.Text = "Status: biblioteca online indisponivel";
            return;
        }

        try
        {
            _loadingLibrary = true;
            LibraryStatusText.Text = "Status: carregando biblioteca online...";
            await _communityClient.InitializeAsync();
            _isCommunityAdmin = IsAdminBuild && await _communityClient.IsAdminAsync();
            HashSet<string> votedAudioIds = (await _communityClient.GetMyVotesAsync())
                .Select(vote => vote.MusicId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            string search = LibrarySearchBox?.Text?.Trim() ?? "";

            _topAudios.Clear();
            int rank = 1;
            foreach (CommunityMusic music in await _communityClient.GetMusicsAsync(search, _communityPage, adminView: false))
            {
                OnlineAudio audio = OnlineAudio.FromCommunity(music, votedAudioIds.Contains(music.Id));
                audio.Rank = rank++;
                _topAudios.Add(audio);
            }

            _adminAudios.Clear();
            if (IsAdminBuild && _isCommunityAdmin)
            {
                foreach (CommunityMusic music in await _communityClient.GetMusicsAsync(search, _adminPage, adminView: true))
                {
                    _adminAudios.Add(OnlineAudio.FromCommunity(music, votedAudioIds.Contains(music.Id)));
                }

                CommunityStats stats = await _communityClient.GetAdminStatsAsync();
                AdminStorageText.Text = $"Uso total: {FormatStorageBytes(stats.TotalBytes)} - Musicas: {stats.TotalMusics}/{stats.MaxMusics}";
            }
            else
            {
                AdminStorageText.Text = "Uso: disponivel apenas no app admin.";
            }

            RefreshLibraryFilter();
            AdminStatusText.Text = IsAdminBuild
                ? _isCommunityAdmin
                    ? $"Area administrativa liberada. User ID: {_communityClient.UserId}"
                    : $"Admin bloqueado. Copie o User ID e cadastre em admin_users: {_communityClient.UserId}"
                : "App de usuario: administracao indisponivel.";
            CommunityUserIdText.Text = $"User ID: {_communityClient.UserId}";
            LibraryStatusText.Text = $"Status: Biblioteca Online - pagina {_communityPage + 1}";
            CommunityStorageText.Text = $"{_topAudios.Count} musicas - Modo {(IsAdminBuild ? "Admin" : "Usuario")}";
        }
        catch (Exception ex)
        {
            LibraryStatusText.Text = "Status: erro ao carregar biblioteca online";
            ShowCopyableErrorDialog("Nao foi possivel carregar a biblioteca online.", IsAdminBuild ? ex.ToString() : GetFriendlySupabaseError(ex));
        }
        finally
        {
            _loadingLibrary = false;
        }
    }

    private void OpenSqlTab_Click(object sender, RoutedEventArgs e)
    {
        if (!IsAdminBuild)
        {
            return;
        }

        LibraryTabs.SelectedIndex = 4;
    }

    private void LibrarySearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        RefreshLibraryFilter();
    }

    private void RefreshLibraryFilter()
    {
        string query = LibrarySearchBox?.Text?.Trim() ?? "";

        ICollectionView topView = CollectionViewSource.GetDefaultView(_topAudios);
        topView.Filter = item => item is OnlineAudio audio && audio.Matches(query);
        topView.Refresh();

        ICollectionView votingView = CollectionViewSource.GetDefaultView(_adminAudios);
        votingView.Filter = item => item is OnlineAudio audio && audio.Matches(query);
        votingView.Refresh();
    }

    private async void PreviewOnline_Click(object sender, RoutedEventArgs e)
    {
        if (GetCommunityAudioFromSender(sender) is not OnlineAudio audio)
        {
            MessageBox.Show("Selecione uma musica para ouvir a previa.", "Biblioteca Online", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            if (_previewDeck.HasAudio && _previewCommunityAudioId == audio.Id)
            {
                _previewDeck.Close();
                _previewCommunityAudioId = null;
                StatusText.Text = "Previa pausada";
                NowPlayingText.Text = "Nenhum som ativo";
                return;
            }

            string localPath = await DownloadSupabaseAudioAsync(audio);
            _previewDeck.Close();
            _previewDeck.Load(localPath, _eqEnabled, _eqBass, _eqMid, _eqTreble);
            _previewDeck.Volume = MasterVolumeSlider.Value / 100.0;
            _previewDeck.Play();
            _previewCommunityAudioId = audio.Id;
            NowPlayingText.Text = $"Previa: {audio.Name}";
            StatusText.Text = "Previa online";
        }
        catch (Exception ex)
        {
            ShowCopyableErrorDialog("Nao foi possivel tocar a previa online.", ex.ToString());
        }
    }

    private async void DownloadOnline_Click(object sender, RoutedEventArgs e)
    {
        if (GetCommunityAudioFromSender(sender) is not OnlineAudio audio)
        {
            MessageBox.Show("Selecione um audio para baixar.", "Biblioteca Online", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await DownloadSupabaseAudioAsync(audio);
            LibraryStatusText.Text = $"Download concluido: {audio.Name}";
            MessageBox.Show("Audio baixado para uso local.", "Biblioteca Online", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ShowCopyableErrorDialog("Nao foi possivel baixar o audio online.", ex.ToString());
        }
    }

    private async void UseOnline_Click(object sender, RoutedEventArgs e)
    {
        if (GetCommunityAudioFromSender(sender) is not OnlineAudio audio)
        {
            MessageBox.Show("Selecione uma musica.", "Biblioteca Online", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_playlists.Count == 0)
        {
            InitializePlaylists();
        }

        PadCard? pad = AskAvailablePadForOnlineAudio(audio);
        if (pad is null)
        {
            return;
        }

        string localPath;
        try
        {
            LibraryStatusText.Text = $"Baixando para o botao {pad.Id}: {audio.Name}";
            localPath = await DownloadSupabaseAudioAsync(audio);
        }
        catch (Exception ex)
        {
            ShowCopyableErrorDialog("Nao foi possivel baixar o audio para usar no botao.", ex.ToString());
            return;
        }

        if (!IsPadAvailableForOnlineAudio(pad))
        {
            MessageBox.Show("Este botao deixou de estar disponivel. Escolha outro botao vazio.", "Biblioteca Online", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        CreateAutomaticBackup("biblioteca-online-card");
        pad.Name = audio.Name;
        pad.SoundPath = localPath;
        pad.OriginalSoundPath = localPath;
        pad.RefreshAudioStatus();
        SaveLocalSettings();
        LibraryStatusText.Text = $"Adicionado ao Botao {pad.Id}: {audio.Name}";
        HideModals();
    }

    private async void LikeAudio_Click(object sender, RoutedEventArgs e)
    {
        if (GetCommunityAudioFromSender(sender) is not OnlineAudio audio)
        {
            MessageBox.Show("Selecione uma musica para votar.", "Votacao", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await VoteOnlineAudioAsync(audio);
    }

    private async void ApproveCommunityMusic_Click(object sender, RoutedEventArgs e)
    {
        if (!IsAdminBuild || !_isCommunityAdmin)
        {
            MessageBox.Show("Somente administrador pode aprovar musicas.", "Administrador", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (VotingAudiosList.SelectedItem is not OnlineAudio audio)
        {
            return;
        }

        try
        {
            await _communityClient.ApproveMusicAsync(audio.Id);
            await LoadSupabaseLibraryAsync();
        }
        catch (Exception ex)
        {
            ShowCopyableErrorDialog("Nao foi possivel aprovar a musica.", ex.ToString());
        }
    }

    private async void DeleteCommunityMusic_Click(object sender, RoutedEventArgs e)
    {
        if (!IsAdminBuild || !_isCommunityAdmin)
        {
            MessageBox.Show("Somente administrador pode excluir musicas.", "Administrador", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (VotingAudiosList.SelectedItem is not OnlineAudio audio)
        {
            return;
        }

        if (MessageBox.Show($"Excluir '{audio.Name}' da biblioteca online?", "Excluir musica", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            await _communityClient.DeleteMusicAsync(audio.Source);
            await LoadSupabaseLibraryAsync();
        }
        catch (Exception ex)
        {
            ShowCopyableErrorDialog("Nao foi possivel excluir a musica.", ex.ToString());
        }
    }

    private void SelectCommunityUploadFile_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Selecionar audio da comunidade",
            Filter = "Audio permitido|*.mp3;*.wav;*.m4a|Todos os arquivos|*.*"
        };

        if (dialog.ShowDialog(this) == true)
        {
            _communityUploadPath = dialog.FileName;
            UploadFileText.Text = dialog.FileName;
            EnsureCommunitySenderName();
            if (string.IsNullOrWhiteSpace(UploadNameBox.Text) || UploadNameBox.Text == "Meu som")
            {
                UploadNameBox.Text = Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    private void ClearCommunityUpload_Click(object sender, RoutedEventArgs e)
    {
        UploadNameBox.Text = "Meu som";
        UploadArtistBox.Text = "Comunidade";
        EnsureCommunitySenderName();
        UploadNoteBox.Text = "";
        UploadFileText.Text = "";
        UploadStatusText.Text = "";
        _communityUploadPath = null;
    }

    private async void UploadAudio_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_communityUploadPath) || !File.Exists(_communityUploadPath))
        {
            UploadStatusText.Text = "Selecione um arquivo de audio antes de enviar.";
            return;
        }

        if (!ValidateCommunityUpload(_communityUploadPath, out TimeSpan duration, out long size, out string error))
        {
            UploadStatusText.Text = error;
            return;
        }

        string title = string.IsNullOrWhiteSpace(UploadNameBox.Text) ? Path.GetFileNameWithoutExtension(_communityUploadPath) : UploadNameBox.Text.Trim();
        string artist = string.IsNullOrWhiteSpace(UploadArtistBox.Text) ? "Comunidade" : UploadArtistBox.Text.Trim();
        string uploadedBy = string.IsNullOrWhiteSpace(UploadSenderBox.Text) ? Environment.UserName : UploadSenderBox.Text.Trim();
        string note = UploadNoteBox.Text.Trim();
        if (uploadedBy.Length > 80)
        {
            UploadStatusText.Text = "O nome em 'Enviado por' deve ter no maximo 80 caracteres.";
            return;
        }

        try
        {
            UploadStatusText.Text = "Enviando para biblioteca online...";
            var progress = new Progress<double>(value => UploadStatusText.Text = $"Enviando {value:0}%");
            await _communityClient.UploadMusicAsync(new NewCommunityMusic(title, artist, uploadedBy, note, (int)Math.Round(duration.TotalSeconds), size), _communityUploadPath, progress);
            UploadStatusText.Text = "Musica enviada para votacao da comunidade.";
            SaveLocalSettings();
            _communityUploadPath = null;
            UploadFileText.Text = "";
            await LoadSupabaseLibraryAsync();
            LibraryTabs.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            ShowCopyableErrorDialog("Nao foi possivel enviar a musica.", ex.ToString());
        }
    }

    private bool ValidateCommunityUpload(string path, out TimeSpan duration, out long size, out string error)
    {
        duration = TimeSpan.Zero;
        size = 0;
        error = "";

        string extension = Path.GetExtension(path).ToLowerInvariant();
        if (extension is not ".mp3" and not ".wav" and not ".m4a")
        {
            error = "Formato nao permitido. Use mp3, wav ou m4a.";
            return false;
        }

        size = new FileInfo(path).Length;
        if (size > MaxCommunityUploadBytes)
        {
            error = "Arquivo acima de 60 MB. Escolha um audio menor.";
            return false;
        }

        try
        {
            using var reader = new AudioFileReader(path);
            duration = reader.TotalTime;
        }
        catch
        {
            error = "Nao foi possivel ler a duracao do audio.";
            return false;
        }

        if (duration.TotalHours > 2)
        {
            error = "Audio acima de 2 horas. Escolha um audio menor.";
            return false;
        }

        return true;
    }

    private async Task VoteOnlineAudioAsync(OnlineAudio audio)
    {
        if (audio.HasVoted)
        {
            MessageBox.Show("Voce ja votou nesta musica.", "Votacao", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            await _communityClient.VoteAsync(audio.Id);
            audio.HasVoted = true;
            audio.DownloadStatus = "Voto registrado";
            MessageBox.Show("Voto registrado. Voce nao podera votar novamente nesta musica.", "Votacao", MessageBoxButton.OK, MessageBoxImage.Information);
            await LoadSupabaseLibraryAsync();
        }
        catch (Exception ex)
        {
            string message = ex.ToString();
            if (message.Contains("duplicate", StringComparison.OrdinalIgnoreCase)
                || message.Contains("23505", StringComparison.OrdinalIgnoreCase))
            {
                audio.HasVoted = true;
                audio.DownloadStatus = "Voce ja votou";
                MessageBox.Show("Voce ja votou nesta musica.", "Votacao", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ShowCopyableErrorDialog("Nao foi possivel registrar o voto.", GetFriendlySupabaseError(ex));
        }
    }

    private async Task<string> DownloadSupabaseAudioAsync(OnlineAudio audio)
    {
        if (!string.IsNullOrWhiteSpace(audio.DownloadedPath) && File.Exists(audio.DownloadedPath))
        {
            audio.IsDownloaded = true;
            audio.DownloadStatus = "Baixado offline";
            return audio.DownloadedPath;
        }

        Directory.CreateDirectory(LocalCommunityMusicDirectory);
        string extension = Path.GetExtension(audio.StoragePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".mp3";
        }

        string localPath = Path.Combine(LocalCommunityMusicDirectory, $"{MakeSafeName(audio.Name)}_{CreateShortHash(audio.Id + audio.StoragePath)}{extension}");
        if (File.Exists(localPath))
        {
            audio.DownloadedPath = localPath;
            audio.IsDownloaded = true;
            audio.DownloadStatus = "Baixado offline";
            return localPath;
        }

        audio.DownloadStatus = "Baixando 0%";
        LibraryStatusText.Text = $"Baixando {audio.Name}: 0%";
        await _communityClient.DownloadMusicAsync(audio.Source, localPath, new Progress<double>(progress =>
        {
            audio.DownloadStatus = $"Baixando {progress:0}%";
            LibraryStatusText.Text = $"Baixando {audio.Name}: {progress:0}%";
        }));

        audio.DownloadedPath = localPath;
        audio.IsDownloaded = true;
        audio.DownloadStatus = "Baixado offline";
        return localPath;
    }

    private void OpenCommunityDownloadsFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(LocalCommunityMusicDirectory);
        Process.Start(new ProcessStartInfo
        {
            FileName = LocalCommunityMusicDirectory,
            UseShellExecute = true
        });
    }

    private OnlineAudio? GetSelectedCommunityAudio()
    {
        return TopAudiosList.SelectedItem as OnlineAudio ?? VotingAudiosList.SelectedItem as OnlineAudio;
    }

    private OnlineAudio? GetCommunityAudioFromSender(object sender)
    {
        return (sender as FrameworkElement)?.Tag as OnlineAudio ?? GetSelectedCommunityAudio();
    }

    private async void PreviousCommunityPage_Click(object sender, RoutedEventArgs e)
    {
        if (_communityPage > 0)
        {
            _communityPage--;
            await LoadSupabaseLibraryAsync();
        }
    }

    private async void NextCommunityPage_Click(object sender, RoutedEventArgs e)
    {
        _communityPage++;
        await LoadSupabaseLibraryAsync();
    }

    private async void PreviousAdminPage_Click(object sender, RoutedEventArgs e)
    {
        if (_adminPage > 0)
        {
            _adminPage--;
            await LoadSupabaseLibraryAsync();
        }
    }

    private async void NextAdminPage_Click(object sender, RoutedEventArgs e)
    {
        _adminPage++;
        await LoadSupabaseLibraryAsync();
    }

    private void CopyCommunityUserId_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_communityClient.UserId))
        {
            Clipboard.SetText(_communityClient.UserId);
            CleanupStatusText.Text = "User ID copiado. Cadastre em public.admin_users para liberar admin.";
        }
    }

    private async void RunCommunityCleanup_Click(object sender, RoutedEventArgs e)
    {
        await RunAdminCleanupAsync(autoRun: false);
    }

    private async Task RunAdminCleanupAsync(bool autoRun)
    {
        if (!IsAdminBuild || !_isCommunityAdmin)
        {
            CleanupStatusText.Text = "Limpeza bloqueada: usuario atual nao e admin.";
            return;
        }

        try
        {
            CleanupStatusText.Text = autoRun ? "Limpeza automatica em andamento..." : "Limpeza em andamento...";
            CommunityCleanupResult result = await _communityClient.RunCleanupAsync(new Progress<string>(message => CleanupStatusText.Text = message));
            CleanupStatusText.Text = $"Limpeza concluida. Total antes: {result.TotalBefore}. Removidas: {result.Removed}. Uso antes: {FormatStorageBytes(result.TotalBytesBefore)}.";
            await LoadSupabaseLibraryAsync();
        }
        catch (Exception ex)
        {
            CleanupStatusText.Text = "Erro na limpeza.";
            ShowCopyableErrorDialog("Nao foi possivel executar a limpeza automatica.", ex.ToString());
        }
    }

    private int AskPadNumber()
    {
        var dialog = new Window
        {
            Title = "Usar no botao",
            Owner = this,
            Width = 320,
            Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(17, 22, 32)),
            ResizeMode = ResizeMode.NoResize
        };

        var input = new TextBox { Text = "1", Margin = new Thickness(16, 8, 16, 10), Padding = new Thickness(8) };
        var ok = new Button { Content = "Aplicar", Margin = new Thickness(16, 0, 16, 14), Padding = new Thickness(10), IsDefault = true };
        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "Numero do pad (1 a 20)",
            Foreground = Brushes.White,
            Margin = new Thickness(16, 16, 16, 0)
        });
        panel.Children.Add(input);
        panel.Children.Add(ok);
        dialog.Content = panel;

        int result = 0;
        ok.Click += (_, _) =>
        {
            int.TryParse(input.Text, out result);
            dialog.Close();
        };

        dialog.ShowDialog();
        return result;
    }

    private PadCard? AskAvailablePadForOnlineAudio(OnlineAudio audio)
    {
        RefreshAllPadAudioStatus();

        var dialog = new Window
        {
            Title = "Escolher botao disponivel",
            Owner = this,
            Width = 680,
            Height = 560,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(17, 22, 32)),
            ResizeMode = ResizeMode.NoResize
        };

        var root = new Grid { Margin = new Thickness(18) };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        header.Children.Add(new TextBlock
        {
            Text = "Escolher botao disponivel",
            Foreground = (Brush)FindResource("TextBrush"),
            FontSize = 22,
            FontWeight = FontWeights.Bold
        });
        header.Children.Add(new TextBlock
        {
            Text = $"Musica: {audio.Name}",
            Foreground = new SolidColorBrush(Color.FromRgb(170, 180, 197)),
            FontSize = 12,
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        header.Children.Add(new TextBlock
        {
            Text = $"Playlist atual: {_playlists[_currentPlaylistIndex].Name}",
            Foreground = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var buttonGrid = new System.Windows.Controls.Primitives.UniformGrid
        {
            Columns = 4,
            Rows = 5
        };

        PadCard? selectedPad = null;
        bool hasAvailablePad = false;
        foreach (PadCard pad in _pads)
        {
            bool available = IsPadAvailableForOnlineAudio(pad);
            bool missing = pad.HasMissingAudio;
            hasAvailablePad |= available;

            string status = available ? "Disponivel" : missing ? "Arquivo ausente" : "Ocupado";
            var content = new StackPanel();
            content.Children.Add(new TextBlock
            {
                Text = $"[{pad.Id}] {pad.Name}",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold,
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            content.Children.Add(new TextBlock
            {
                Text = status,
                Foreground = available
                    ? new SolidColorBrush(Color.FromRgb(103, 232, 249))
                    : missing
                        ? new SolidColorBrush(Color.FromRgb(251, 113, 133))
                        : new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontSize = 11,
                Margin = new Thickness(0, 6, 0, 0)
            });

            var button = new Button
            {
                Content = content,
                Tag = pad,
                IsEnabled = true,
                IsHitTestVisible = available,
                Focusable = available,
                Height = 76,
                Margin = new Thickness(4),
                Padding = new Thickness(10),
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = available
                    ? new SolidColorBrush(Color.FromRgb(29, 78, 216))
                    : new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = available
                    ? new SolidColorBrush(Color.FromRgb(34, 211, 238))
                    : new SolidColorBrush(Color.FromRgb(38, 48, 65)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(1),
                Cursor = available ? Cursors.Hand : Cursors.Arrow
            };

            if (available)
            {
                button.Click += (_, _) =>
                {
                    selectedPad = (PadCard)button.Tag;
                    dialog.DialogResult = true;
                };
            }

            buttonGrid.Children.Add(button);
        }

        var scroll = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = buttonGrid
        };
        Grid.SetRow(scroll, 1);
        root.Children.Add(scroll);

        var footer = new Grid { Margin = new Thickness(0, 16, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var statusText = new TextBlock
        {
            Text = hasAvailablePad
                ? "Selecione um botao vazio para adicionar esta musica."
                : "Nao ha botoes disponiveis nesta playlist.",
            Foreground = hasAvailablePad
                ? new SolidColorBrush(Color.FromRgb(170, 180, 197))
                : new SolidColorBrush(Color.FromRgb(251, 191, 36)),
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        footer.Children.Add(statusText);

        var cancel = new Button
        {
            Content = "Cancelar",
            Style = (Style)FindResource("RoundedButton"),
            Width = 110,
            Height = 38,
            IsCancel = true
        };
        Grid.SetColumn(cancel, 1);
        footer.Children.Add(cancel);

        Grid.SetRow(footer, 2);
        root.Children.Add(footer);

        dialog.Content = root;
        return dialog.ShowDialog() == true ? selectedPad : null;
    }

    private static bool IsPadAvailableForOnlineAudio(PadCard pad)
    {
        return string.IsNullOrWhiteSpace(pad.SoundPath);
    }

    private void EnsureCommunitySenderName()
    {
        if (UploadSenderBox is not null && string.IsNullOrWhiteSpace(UploadSenderBox.Text))
        {
            UploadSenderBox.Text = Environment.UserName;
        }
    }

    private static string FormatStorageBytes(long value)
    {
        return value >= 1024 * 1024
            ? $"{value / 1024d / 1024d:0.0} MB"
            : $"{Math.Max(1, value / 1024)} KB";
    }

    private PadCard? AskPlaylistAndPad()
    {
        var dialog = new Window
        {
            Title = "Usar no botao",
            Owner = this,
            Width = 420,
            Height = 260,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(17, 22, 32)),
            ResizeMode = ResizeMode.NoResize
        };

        var playlistBox = new ComboBox { Margin = new Thickness(16, 6, 16, 14), Height = 34 };
        playlistBox.Items.Add(PlaylistNameText.Text);
        playlistBox.SelectedIndex = 0;

        var padBox = new ComboBox { Margin = new Thickness(16, 6, 16, 18), Height = 34 };
        foreach (PadCard pad in _pads)
        {
            padBox.Items.Add($"Botao {pad.Id} - {pad.Name}");
        }

        padBox.SelectedIndex = _editingPad is null ? 0 : Math.Clamp(_editingPad.Id - 1, 0, _pads.Count - 1);

        var ok = new Button
        {
            Content = "Usar neste botao",
            Margin = new Thickness(16, 0, 8, 0),
            Padding = new Thickness(10),
            Width = 140,
            IsDefault = true
        };

        var cancel = new Button
        {
            Content = "Cancelar",
            Margin = new Thickness(8, 0, 16, 0),
            Padding = new Thickness(10),
            Width = 100,
            IsCancel = true
        };

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        buttons.Children.Add(ok);
        buttons.Children.Add(cancel);

        var panel = new StackPanel();
        panel.Children.Add(new TextBlock
        {
            Text = "Playlist disponivel",
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(16, 16, 16, 0)
        });
        panel.Children.Add(playlistBox);
        panel.Children.Add(new TextBlock
        {
            Text = "Escolha o botao",
            Foreground = Brushes.White,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(16, 0, 16, 0)
        });
        panel.Children.Add(padBox);
        panel.Children.Add(buttons);
        dialog.Content = panel;

        PadCard? selectedPad = null;
        ok.Click += (_, _) =>
        {
            int index = padBox.SelectedIndex;
            if (index >= 0 && index < _pads.Count)
            {
                selectedPad = _pads[index];
            }

            dialog.Close();
        };

        dialog.ShowDialog();
        return selectedPad;
    }

    private void OpenSoundsFolder_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(LocalSoundsDirectory);
        Process.Start(new ProcessStartInfo { FileName = LocalSoundsDirectory, UseShellExecute = true });
    }

    private void OpenAutomaticBackups_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(LocalBackupsDirectory);
        Process.Start(new ProcessStartInfo { FileName = LocalBackupsDirectory, UseShellExecute = true });
    }

    private void OpenFirstRun_Click(object sender, RoutedEventArgs e)
    {
        ShowFirstRunOverlay();
    }

    private void CloseFirstRun_Click(object sender, RoutedEventArgs e)
    {
        HideModals();
    }

    private void CompleteFirstRun_Click(object sender, RoutedEventArgs e)
    {
        MasterVolumeSlider.Value = Math.Clamp(FirstRunVolumeSlider.Value, 0, 100);
        string playlistName = FirstRunPlaylistBox.Text.Trim();
        if (!string.IsNullOrWhiteSpace(playlistName) && _playlists.Count > 0)
        {
            CaptureCurrentPlaylist();
            _playlists[_currentPlaylistIndex] = _playlists[_currentPlaylistIndex].Clone(playlistName);
            UpdatePlaylistTitle();
        }

        _firstRunCompleted = true;
        SaveLocalSettings();
        HideModals();
    }

    private void ShowFirstRunOverlay()
    {
        FirstRunVolumeSlider.Value = MasterVolumeSlider?.Value ?? 80;
        FirstRunPlaylistBox.Text = _playlists.Count > 0 ? _playlists[_currentPlaylistIndex].Name : DefaultPlaylistName;
        ShowModal(FirstRunOverlay);
    }

    private void CheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        string version = GetAppVersion();
        if (string.IsNullOrWhiteSpace(ManualUpdateUrl))
        {
            MessageBox.Show($"Versao atual: {version}\n\nAtualizacoes serao informadas pelo distribuidor do app.", "Verificar atualizacao", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show($"Versao atual: {version}\n\nAbrir pagina de atualizacao?", "Verificar atualizacao", MessageBoxButton.YesNo, MessageBoxImage.Information) == MessageBoxResult.Yes)
        {
            OpenExternalUrl(ManualUpdateUrl);
        }
    }

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Exportar diagnostico",
            Filter = "Texto|*.txt",
            FileName = $"som-de-fundo-diagnostico-{DateTime.Now:yyyyMMdd-HHmmss}.txt"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        File.WriteAllText(dialog.FileName, BuildDiagnosticsReport(), Encoding.UTF8);
        MessageBox.Show("Diagnostico exportado.", "Diagnostico", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LocateMissingPadAudio(PadCard pad)
    {
        if (MessageBox.Show(this, $"O arquivo do {pad.Name} nao foi encontrado.\nDeseja localizar o audio novamente?", "Arquivo nao encontrado", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Localizar arquivo de audio",
            Filter = "Audio|*.mp3;*.wav;*.ogg|Todos os arquivos|*.*"
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            CreateAutomaticBackup("localizar-arquivo-ausente");
            string? savedPath = SaveFileInsideApp(dialog.FileName, LocalSoundsDirectory);
            pad.SoundPath = savedPath;
            pad.OriginalSoundPath = savedPath;
            pad.RefreshAudioStatus();
            SaveLocalSettings();
            MessageBox.Show("Arquivo localizado e vinculado ao botao.", "Arquivo localizado", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Nao foi possivel vincular o arquivo.\n{ex.Message}", "Arquivo nao encontrado", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RemoteButton_Click(object sender, RoutedEventArgs e)
    {
        StartRemoteServer();
        RefreshRemotePanel();
        ShowModal(RemoteOverlay);
        SetRemoteButtonHighlighted(true);
    }

    private void CloseRemote_Click(object sender, RoutedEventArgs e)
    {
        HideModals();
    }

    private void StartRemoteServer_Click(object sender, RoutedEventArgs e)
    {
        StartRemoteServer();
        RefreshRemotePanel();
    }

    private void StopRemoteServer_Click(object sender, RoutedEventArgs e)
    {
        StopRemoteServer();
        RefreshRemotePanel();
    }

    private void OpenRemoteBrowser_Click(object sender, RoutedEventArgs e)
    {
        StartRemoteServer();
        RefreshRemotePanel();
        Process.Start(new ProcessStartInfo { FileName = GetRemoteAccessUrl(), UseShellExecute = true });
    }

    private void CopyRemoteUrl_Click(object sender, RoutedEventArgs e)
    {
        StartRemoteServer();
        RefreshRemotePanel();
        Clipboard.SetText(GetRemoteAccessUrl());
        RemoteStatusText.Text = "URL copiada. Cole no celular ou leia o QR Code.";
    }

    private void ChangeRemotePin_Click(object sender, RoutedEventArgs e)
    {
        PinBox.Text = RandomNumberGenerator.GetInt32(100000, 999999).ToString();
        SaveLocalSettings();
        RefreshRemotePanel();
    }

    private void StartRemoteServer(bool showError = true)
    {
        if (_remoteServer is not null)
        {
            return;
        }

        try
        {
            _remoteCancellation = new CancellationTokenSource();
            _remoteServer = new TcpListener(IPAddress.Any, RemotePort);
            _remoteServer.Start();
            _ = Task.Run(() => RemoteAcceptLoopAsync(_remoteCancellation.Token));
        }
        catch (Exception ex)
        {
            StopRemoteServer();
            if (showError)
            {
                ShowCopyableErrorDialog("Nao foi possivel ligar o controle remoto.", ex.Message);
            }
        }
    }

    private void StopRemoteServer()
    {
        _remoteCancellation?.Cancel();
        _remoteCancellation?.Dispose();
        _remoteCancellation = null;
        _remoteServer?.Stop();
        _remoteServer = null;
        _remoteConnections = 0;
    }

    private async Task RemoteAcceptLoopAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested && _remoteServer is not null)
        {
            try
            {
                TcpClient client = await _remoteServer.AcceptTcpClientAsync(token);
                _ = Task.Run(() => HandleRemoteClientAsync(client, token), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (!token.IsCancellationRequested)
                {
                    await Dispatcher.InvokeAsync(RefreshRemotePanel);
                }
            }
        }
    }

    private async Task HandleRemoteClientAsync(TcpClient client, CancellationToken token)
    {
        Interlocked.Increment(ref _remoteConnections);
        await RefreshRemotePanelSafeAsync();
        NetworkStream? stream = null;

        try
        {
            using (client)
            {
                stream = client.GetStream();
                RemoteHttpRequest request = await ReadRemoteHttpRequestAsync(stream, token);
                string rawPath = request.RawTarget;
                Uri uri = CreateRemoteRequestUri(rawPath);
                string path = uri.AbsolutePath.TrimEnd('/');
                string message = "";

                if (path.Equals("/favicon.ico", StringComparison.OrdinalIgnoreCase))
                {
                    await WriteRemoteResponseAsync(stream, "", "text/plain", token, 204);
                    return;
                }

                string expectedPin = await Dispatcher.InvokeAsync(() => PinBox.Text.Trim());
                bool authenticated = ValidateRemotePin(uri, expectedPin);
                if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase))
                {
                    await HandleRemoteApiAsync(request, stream, uri, path, authenticated, token);
                    return;
                }

                if (authenticated && path.Equals("/play", StringComparison.OrdinalIgnoreCase))
                {
                    if (int.TryParse(GetQueryValue(uri, "id"), out int id))
                    {
                        await Dispatcher.InvokeAsync(() => PlayPadById(id));
                        message = $"Botao {id} acionado.";
                    }
                }
                else if (authenticated && path.Equals("/stop", StringComparison.OrdinalIgnoreCase))
                {
                    await Dispatcher.InvokeAsync(() => StopPlayback());
                    message = "Reproducao parada.";
                }
                else if (authenticated && path.Equals("/pause", StringComparison.OrdinalIgnoreCase))
                {
                    await Dispatcher.InvokeAsync(() => PauseResume_Click(this, new RoutedEventArgs()));
                    message = "Pausa/retorno acionado.";
                }
                else if (!authenticated && !string.IsNullOrWhiteSpace(uri.Query))
                {
                    message = "PIN incorreto.";
                }

                string html = await Dispatcher.InvokeAsync(() => BuildRemotePage(authenticated, message));
                await WriteRemoteResponseAsync(stream, html, "text/html; charset=utf-8", token);
            }
        }
        catch (Exception ex)
        {
            if (stream is not null && client.Connected)
            {
                try
                {
                    string body = $"Controle remoto indisponivel: {WebUtility.HtmlEncode(ex.Message)}";
                    await WriteRemoteResponseAsync(stream, body, "text/plain; charset=utf-8", CancellationToken.None, 500);
                }
                catch
                {
                    // Cliente remoto desconectado.
                }
            }
        }
        finally
        {
            Interlocked.Decrement(ref _remoteConnections);
            await RefreshRemotePanelSafeAsync();
        }
    }

    private async Task RefreshRemotePanelSafeAsync()
    {
        try
        {
            await Dispatcher.InvokeAsync(RefreshRemotePanel);
        }
        catch
        {
            // A resposta HTTP do celular nao deve falhar por uma atualizacao visual do modal.
        }
    }

    private static Uri CreateRemoteRequestUri(string rawTarget)
    {
        if (Uri.TryCreate(rawTarget, UriKind.Absolute, out Uri? absoluteUri))
        {
            return absoluteUri;
        }

        if (string.IsNullOrWhiteSpace(rawTarget))
        {
            rawTarget = "/";
        }

        if (!rawTarget.StartsWith('/'))
        {
            rawTarget = "/" + rawTarget;
        }

        return new Uri($"http://localhost{rawTarget}");
    }

    private async Task HandleRemoteApiAsync(RemoteHttpRequest request, NetworkStream stream, Uri uri, string path, bool authenticated, CancellationToken token)
    {
        if (!authenticated)
        {
            if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) && path.Equals("/api/app-icon", StringComparison.OrdinalIgnoreCase))
            {
                await WritePackResourceResponseAsync(stream, "ICONE SOM DE FUNDO.png", "image/png", token);
                return;
            }

            await WriteJsonResponseAsync(stream, new { ok = false, error = "PIN invalido." }, token, 403);
            return;
        }

        if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) && path.Equals("/api/app-icon", StringComparison.OrdinalIgnoreCase))
        {
            await WritePackResourceResponseAsync(stream, "ICONE SOM DE FUNDO.png", "image/png", token);
            return;
        }

        if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) && path.Equals("/api/cover", StringComparison.OrdinalIgnoreCase))
        {
            int playlistIndex = ParseQueryInt(uri, "playlist", _currentPlaylistIndex);
            int padId = ParseQueryInt(uri, "pad", _currentPad?.Id ?? 0);
            string? coverPath = await Dispatcher.InvokeAsync(() => GetRemoteCoverPath(playlistIndex, padId));
            if (coverPath is not null)
            {
                await WriteFileResponseAsync(request, stream, coverPath, GetContentType(coverPath), token, allowRanges: false);
            }
            else
            {
                await WritePackResourceResponseAsync(stream, "Assets/default-music-cover.png", "image/png", token);
            }

            return;
        }

        if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) && path.Equals("/api/preview", StringComparison.OrdinalIgnoreCase))
        {
            int playlistIndex = ParseQueryInt(uri, "playlist", _currentPlaylistIndex);
            int padId = ParseQueryInt(uri, "pad", 0);
            string? audioPath = await Dispatcher.InvokeAsync(() => GetRemoteAudioPath(playlistIndex, padId));
            if (audioPath is null)
            {
                await WriteJsonResponseAsync(stream, new { ok = false, error = "Audio nao encontrado." }, token, 404);
                return;
            }

            await WriteFileResponseAsync(request, stream, audioPath, GetContentType(audioPath), token, allowRanges: true);
            return;
        }

        if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) && path.Equals("/api/state", StringComparison.OrdinalIgnoreCase))
        {
            object state = await Dispatcher.InvokeAsync(BuildRemoteState);
            await WriteJsonResponseAsync(stream, state, token);
            return;
        }

        if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) && path.Equals("/api/button", StringComparison.OrdinalIgnoreCase))
        {
            int playlistIndex = ParseQueryInt(uri, "playlist", _currentPlaylistIndex);
            int padId = ParseQueryInt(uri, "pad", 0);
            object result = await Dispatcher.InvokeAsync(() => BuildRemoteButtonInfo(playlistIndex, padId));
            await WriteJsonResponseAsync(stream, result, token);
            return;
        }

        if (request.Method.Equals("GET", StringComparison.OrdinalIgnoreCase) && path.Equals("/api/upload-result", StringComparison.OrdinalIgnoreCase))
        {
            string key = CreateUploadKey(ParseQueryInt(uri, "playlist", _currentPlaylistIndex), ParseQueryInt(uri, "pad", 0));
            RemoteUploadStatus status;
            lock (_remoteUploadLock)
            {
                status = _remoteUploadStatuses.TryGetValue(key, out RemoteUploadStatus? existing)
                    ? existing
                    : new RemoteUploadStatus("idle", 0, "");
            }

            await WriteJsonResponseAsync(stream, new { ok = true, status.Status, status.Percent, status.Message }, token);
            return;
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.Equals("/api/select-playlist", StringComparison.OrdinalIgnoreCase))
        {
            int playlistIndex = ParseQueryInt(uri, "index", -1);
            (bool ok, object result) = await Dispatcher.InvokeAsync(() =>
            {
                bool ok = SelectRemotePlaylist(playlistIndex);
                return (ok, ok ? BuildRemoteState() : new { ok = false, error = "Playlist invalida." });
            });
            await WriteJsonResponseAsync(stream, result, token, ok ? 200 : 400);
            return;
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.Equals("/api/volume", StringComparison.OrdinalIgnoreCase))
        {
            int value = ParseQueryInt(uri, "value", -1);
            bool ok = await Dispatcher.InvokeAsync(() =>
            {
                if (value < 0 || value > 100)
                {
                    return false;
                }

                MasterVolumeSlider.Value = value;
                SaveLocalSettings();
                return true;
            });
            await WriteJsonResponseAsync(stream, ok ? new { ok = true, volume = value } : new { ok = false, error = "Volume invalido." }, token, ok ? 200 : 400);
            return;
        }

        if (request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) && path.Equals("/api/upload", StringComparison.OrdinalIgnoreCase))
        {
            await HandleRemoteUploadAsync(request, stream, uri, token);
            return;
        }

        await WriteJsonResponseAsync(stream, new { ok = false, error = "Endpoint nao encontrado." }, token, 404);
    }

    private object BuildRemoteState()
    {
        CaptureCurrentPlaylist();
        TimeSpan duration = _activeDeck.Duration;
        TimeSpan position = _activeDeck.Position;
        double progress = duration.TotalSeconds > 0
            ? Math.Clamp(position.TotalSeconds / duration.TotalSeconds * 100, 0, 100)
            : PlaybackProgress.Value;
        string playbackStatus = _currentPad is null ? "Parado" : _isPaused ? "Pausado" : "Tocando";

        return new
        {
            ok = true,
            appName = "Som de Fundo Pro",
            playlistIndex = _currentPlaylistIndex,
            playlists = _playlists.Select((playlist, index) => new { id = index, playlist.Name }).ToList(),
            current = NowPlayingText.Text,
            status = playbackStatus,
            statusText = StatusText.Text,
            isPaused = _isPaused,
            currentPadId = _currentPad?.Id,
            currentCoverUrl = _currentPad is not null ? $"/api/cover?playlist={_currentPlaylistIndex}&pad={_currentPad.Id}" : "/api/cover",
            progress,
            positionSeconds = Math.Max(0, position.TotalSeconds),
            durationSeconds = Math.Max(0, duration.TotalSeconds),
            volume = (int)Math.Round(MasterVolumeSlider.Value),
            pads = _pads.Select(pad => new
            {
                id = pad.Id,
                pad.Name,
                pad.Color,
                hasAudio = IsRealAudioPath(pad.SoundPath) && File.Exists(pad.SoundPath),
                isPlaying = pad.IsPlaying,
                canUpload = !IsRealAudioPath(pad.SoundPath) || !File.Exists(pad.SoundPath),
                previewUrl = IsRealAudioPath(pad.SoundPath) && File.Exists(pad.SoundPath) ? $"/api/preview?playlist={_currentPlaylistIndex}&pad={pad.Id}" : null
            }).ToList()
        };
    }

    private string? GetRemoteCoverPath(int playlistIndex, int padId)
    {
        CaptureCurrentPlaylist();
        PadBackup? pad = playlistIndex >= 0 && playlistIndex < _playlists.Count && padId >= 1
            ? _playlists[playlistIndex].Pads.FirstOrDefault(item => item.Id == padId)
            : null;

        return CoverImageService.IsUsableCustomCover(pad?.CoverPath) ? Path.GetFullPath(pad!.CoverPath!) : null;
    }

    private string? GetRemoteAudioPath(int playlistIndex, int padId)
    {
        CaptureCurrentPlaylist();
        if (playlistIndex < 0 || playlistIndex >= _playlists.Count || padId < 1 || padId > _pads.Count)
        {
            return null;
        }

        PadBackup? pad = _playlists[playlistIndex].Pads.FirstOrDefault(item => item.Id == padId);
        if (!IsRealAudioPath(pad?.SoundPath) || !File.Exists(pad!.SoundPath))
        {
            return null;
        }

        return Path.GetFullPath(pad.SoundPath!);
    }

    private object BuildRemoteButtonInfo(int playlistIndex, int padId)
    {
        if (playlistIndex < 0 || playlistIndex >= _playlists.Count || padId < 1 || padId > _pads.Count)
        {
            return new { ok = false, error = "Playlist ou botao invalido." };
        }

        PadBackup? pad = _playlists[playlistIndex].Pads.FirstOrDefault(item => item.Id == padId);
        bool hasAudio = IsRealAudioPath(pad?.SoundPath) && File.Exists(pad!.SoundPath);
        return new { ok = true, playlistIndex, padId, hasAudio, name = pad?.Name ?? $"Botao {padId}" };
    }

    private bool SelectRemotePlaylist(int playlistIndex)
    {
        if (playlistIndex < 0 || playlistIndex >= _playlists.Count)
        {
            return false;
        }

        CaptureCurrentPlaylist();
        StopPlayback(useFade: false);
        _currentPlaylistIndex = playlistIndex;
        ApplyCurrentPlaylist();
        SaveLocalSettings();
        return true;
    }

    private async Task HandleRemoteUploadAsync(RemoteHttpRequest request, NetworkStream stream, Uri uri, CancellationToken token)
    {
        int playlistIndex = ParseQueryInt(uri, "playlist", -1);
        int padId = ParseQueryInt(uri, "pad", -1);
        string originalName = WebUtility.UrlDecode(GetQueryValue(uri, "name"));
        string contentType = request.Headers.TryGetValue("content-type", out string? type) ? type : "";
        string uploadKey = CreateUploadKey(playlistIndex, padId);

        if (!TryBeginRemoteUpload(uploadKey, out string? busyError))
        {
            await WriteJsonResponseAsync(stream, new { ok = false, error = busyError }, token, 409);
            return;
        }

        string? tempPath = null;
        string? finalPath = null;

        try
        {
            RemoteUploadTarget target = await Dispatcher.InvokeAsync(() => ValidateRemoteUploadTarget(playlistIndex, padId));
            ValidateRemoteUploadMetadata(originalName, contentType, request.ContentLength);

            Directory.CreateDirectory(LocalSoundsDirectory);
            string extension = Path.GetExtension(originalName).ToLowerInvariant();
            string safeBaseName = MakeSafeFileBaseName(Path.GetFileNameWithoutExtension(originalName));
            tempPath = CreateUniqueManagedPath(LocalSoundsDirectory, $"{safeBaseName}_upload", ".tmp");
            finalPath = CreateUniqueManagedPath(LocalSoundsDirectory, safeBaseName, extension);

            SetRemoteUploadStatus(uploadKey, "uploading", 0, "Enviando...");
            await SaveRemoteUploadBodyAsync(request, stream, tempPath, uploadKey, token);
            ValidateSavedAudioFile(tempPath, extension, contentType);
            File.Move(tempPath, finalPath, overwrite: false);
            tempPath = null;

            string displayName = Path.GetFileNameWithoutExtension(originalName);
            await Dispatcher.InvokeAsync(() =>
            {
                CreateAutomaticBackup("upload-remoto");
                LinkRemoteUploadedAudio(playlistIndex, padId, finalPath, displayName);
            });
            SetRemoteUploadStatus(uploadKey, "success", 100, "Musica adicionada ao botao.");
            await WriteJsonResponseAsync(stream, new { ok = true, playlist = target.PlaylistName, padId, fileName = Path.GetFileName(finalPath), displayName }, token);
        }
        catch (Exception ex)
        {
            SetRemoteUploadStatus(uploadKey, "error", 0, ex.Message);
            TryDeleteFile(tempPath);
            TryDeleteFile(finalPath);
            await WriteJsonResponseAsync(stream, new { ok = false, error = ex.Message }, token, 400);
        }
        finally
        {
            EndRemoteUpload(uploadKey);
        }
    }

    private RemoteUploadTarget ValidateRemoteUploadTarget(int playlistIndex, int padId)
    {
        CaptureCurrentPlaylist();
        if (playlistIndex < 0 || playlistIndex >= _playlists.Count)
        {
            throw new InvalidOperationException("Playlist invalida.");
        }

        if (padId < 1 || padId > _pads.Count)
        {
            throw new InvalidOperationException("Botao invalido.");
        }

        PadBackup? pad = _playlists[playlistIndex].Pads.FirstOrDefault(item => item.Id == padId);
        if (pad is null)
        {
            throw new InvalidOperationException("Botao nao encontrado na playlist.");
        }

        if (IsRealAudioPath(pad.SoundPath) && File.Exists(pad.SoundPath))
        {
            throw new InvalidOperationException("Este botao ja possui musica.");
        }

        return new RemoteUploadTarget(_playlists[playlistIndex].Name, pad.Name);
    }

    private static void ValidateRemoteUploadMetadata(string originalName, string contentType, long contentLength)
    {
        if (contentLength <= 0)
        {
            throw new InvalidOperationException("Arquivo vazio ou sem tamanho informado.");
        }

        if (contentLength > MaxRemoteUploadBytes)
        {
            throw new InvalidOperationException("Arquivo acima do limite de 800 MB.");
        }

        string fileName = Path.GetFileName(originalName);
        if (string.IsNullOrWhiteSpace(fileName)
            || originalName.Contains("..", StringComparison.Ordinal)
            || originalName.Contains('/', StringComparison.Ordinal)
            || originalName.Contains('\\', StringComparison.Ordinal)
            || Path.IsPathRooted(originalName))
        {
            throw new InvalidOperationException("Nome de arquivo invalido.");
        }

        string extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (extension is not ".mp3" and not ".wav" and not ".ogg")
        {
            throw new InvalidOperationException("Formato invalido. Use MP3, WAV ou OGG.");
        }

        string normalizedType = contentType.Split(';')[0].Trim().ToLowerInvariant();
        bool mimeOk = extension switch
        {
            ".mp3" => normalizedType is "audio/mpeg" or "audio/mp3" or "audio/x-mpeg",
            ".wav" => normalizedType is "audio/wav" or "audio/x-wav" or "audio/wave" or "audio/vnd.wave",
            ".ogg" => normalizedType is "audio/ogg" or "application/ogg" or "audio/vorbis",
            _ => false
        };

        if (!mimeOk)
        {
            throw new InvalidOperationException("Tipo MIME invalido para o formato enviado.");
        }
    }

    private async Task SaveRemoteUploadBodyAsync(RemoteHttpRequest request, NetworkStream stream, string tempPath, string uploadKey, CancellationToken token)
    {
        long remaining = request.ContentLength;
        long written = 0;
        await using var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, useAsync: true);

        if (request.BodyPrefix.Length > 0)
        {
            int prefixCount = (int)Math.Min(request.BodyPrefix.Length, remaining);
            await output.WriteAsync(request.BodyPrefix.AsMemory(0, prefixCount), token);
            written += prefixCount;
            remaining -= prefixCount;
        }

        var buffer = new byte[128 * 1024];
        while (remaining > 0)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), token);
            if (read <= 0)
            {
                throw new InvalidOperationException("Conexao interrompida durante o envio.");
            }

            await output.WriteAsync(buffer.AsMemory(0, read), token);
            written += read;
            remaining -= read;
            int percent = (int)Math.Clamp(written * 100 / Math.Max(1, request.ContentLength), 0, 100);
            SetRemoteUploadStatus(uploadKey, "uploading", percent, $"Enviando... {percent}%");
        }
    }

    private static void ValidateSavedAudioFile(string path, string extension, string contentType)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length <= 0)
        {
            throw new InvalidOperationException("Arquivo vazio.");
        }

        if (info.Length > MaxRemoteUploadBytes)
        {
            throw new InvalidOperationException("Arquivo acima do limite de 800 MB.");
        }

        Span<byte> header = stackalloc byte[16];
        using (var input = File.OpenRead(path))
        {
            int read = input.Read(header);
            if (read < 4)
            {
                throw new InvalidOperationException("Arquivo invalido.");
            }
        }

        bool valid = extension switch
        {
            ".mp3" => IsMp3Header(header),
            ".wav" => header.StartsWith("RIFF"u8) && header[8..].StartsWith("WAVE"u8),
            ".ogg" => header.StartsWith("OggS"u8),
            _ => false
        };

        if (!valid)
        {
            throw new InvalidOperationException("Conteudo do arquivo nao corresponde ao formato informado.");
        }
    }

    private static bool IsMp3Header(ReadOnlySpan<byte> header)
    {
        return header.StartsWith("ID3"u8) || (header.Length >= 2 && header[0] == 0xFF && (header[1] & 0xE0) == 0xE0);
    }

    private void LinkRemoteUploadedAudio(int playlistIndex, int padId, string savedPath, string displayName)
    {
        CaptureCurrentPlaylist();
        PadBackup? backup = _playlists[playlistIndex].Pads.FirstOrDefault(item => item.Id == padId);
        if (backup is null)
        {
            throw new InvalidOperationException("Botao nao encontrado.");
        }

        if (IsRealAudioPath(backup.SoundPath) && File.Exists(backup.SoundPath))
        {
            throw new InvalidOperationException("Este botao ja possui musica.");
        }

        backup.SoundPath = savedPath;
        backup.OriginalSoundPath = savedPath;
        if (string.IsNullOrWhiteSpace(backup.Name) || backup.Name.Equals($"Botao {padId}", StringComparison.OrdinalIgnoreCase))
        {
            backup.Name = string.IsNullOrWhiteSpace(displayName) ? $"Botao {padId}" : displayName;
        }

        if (playlistIndex == _currentPlaylistIndex)
        {
            ApplyCurrentPlaylist();
        }
        else
        {
            UpdatePlaylistTitle();
        }

        SaveLocalSettings();
    }

    private bool TryBeginRemoteUpload(string key, out string? error)
    {
        lock (_remoteUploadLock)
        {
            if (_remoteUploadKeys.Contains(key))
            {
                error = "Ja existe um envio em andamento para este botao.";
                return false;
            }

            _remoteUploadKeys.Add(key);
            _remoteUploadStatuses[key] = new RemoteUploadStatus("starting", 0, "Preparando envio...");
            error = null;
            return true;
        }
    }

    private void EndRemoteUpload(string key)
    {
        lock (_remoteUploadLock)
        {
            _remoteUploadKeys.Remove(key);
        }
    }

    private void SetRemoteUploadStatus(string key, string status, int percent, string message)
    {
        lock (_remoteUploadLock)
        {
            _remoteUploadStatuses[key] = new RemoteUploadStatus(status, Math.Clamp(percent, 0, 100), message);
        }
    }

    private static string CreateUploadKey(int playlistIndex, int padId) => $"{playlistIndex}:{padId}";

    private static string MakeSafeFileBaseName(string? name)
    {
        string safe = string.Join("_", (name ?? "audio").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        safe = Regex.Replace(safe, @"[^A-Za-z0-9._-]+", "_").Trim('.', '_');
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "audio";
        }

        return safe.Length > 60 ? safe[..60] : safe;
    }

    private static string CreateUniqueManagedPath(string targetDirectory, string baseName, string extension)
    {
        string fullDirectory = Path.GetFullPath(targetDirectory);
        for (int attempt = 0; attempt < 30; attempt++)
        {
            string suffix = $"{DateTime.Now:yyyyMMddHHmmssfff}_{Random.Shared.Next(1000, 9999)}";
            string candidate = Path.GetFullPath(Path.Combine(fullDirectory, $"{baseName}_{suffix}{extension}"));
            if (!candidate.StartsWith(fullDirectory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Caminho de destino invalido.");
            }

            if (!File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Nao foi possivel gerar nome unico para o arquivo.");
    }

    private static void TryDeleteFile(string? path)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Arquivo incompleto pode ja ter sido removido.
        }
    }

    private static async Task<RemoteHttpRequest> ReadRemoteHttpRequestAsync(NetworkStream stream, CancellationToken token)
    {
        var received = new MemoryStream();
        var buffer = new byte[8192];
        int headerEnd = -1;

        while (headerEnd < 0)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (read <= 0)
            {
                throw new InvalidOperationException("Requisicao remota incompleta.");
            }

            received.Write(buffer, 0, read);
            if (received.Length > MaxRemoteHeaderBytes)
            {
                throw new InvalidOperationException("Cabecalho HTTP muito grande.");
            }

            headerEnd = IndexOfHeaderEnd(received.GetBuffer(), (int)received.Length);
        }

        byte[] allBytes = received.ToArray();
        string headerText = Encoding.UTF8.GetString(allBytes, 0, headerEnd);
        string[] lines = headerText.Split(["\r\n", "\n"], StringSplitOptions.None);
        string[] firstLineParts = lines.FirstOrDefault()?.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? [];
        if (firstLineParts.Length < 2)
        {
            throw new InvalidOperationException("Linha HTTP invalida.");
        }

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string line in lines.Skip(1))
        {
            int separator = line.IndexOf(':');
            if (separator > 0)
            {
                headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
            }
        }

        long contentLength = 0;
        if (headers.TryGetValue("content-length", out string? lengthText) && !long.TryParse(lengthText, out contentLength))
        {
            throw new InvalidOperationException("Content-Length invalido.");
        }

        int bodyStart = headerEnd + 4;
        byte[] bodyPrefix = bodyStart < allBytes.Length ? allBytes[bodyStart..] : [];
        if (bodyPrefix.Length > contentLength && contentLength >= 0)
        {
            bodyPrefix = bodyPrefix[..(int)contentLength];
        }

        return new RemoteHttpRequest(firstLineParts[0], firstLineParts[1], headers, bodyPrefix, contentLength);
    }

    private static int IndexOfHeaderEnd(byte[] buffer, int length)
    {
        for (int i = 3; i < length; i++)
        {
            if (buffer[i - 3] == '\r' && buffer[i - 2] == '\n' && buffer[i - 1] == '\r' && buffer[i] == '\n')
            {
                return i - 3;
            }
        }

        return -1;
    }

    private static int ParseQueryInt(Uri uri, string key, int fallback)
    {
        return int.TryParse(GetQueryValue(uri, key), out int value) ? value : fallback;
    }

    private static async Task WriteJsonResponseAsync(NetworkStream stream, object payload, CancellationToken token, int statusCode = 200)
    {
        string json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        await WriteRemoteResponseAsync(stream, json, "application/json; charset=utf-8", token, statusCode);
    }

    private static async Task WritePackResourceResponseAsync(NetworkStream stream, string resourcePath, string contentType, CancellationToken token)
    {
        Uri uri = new($"pack://application:,,,/{resourcePath}", UriKind.Absolute);
        var resource = Application.GetResourceStream(uri);
        if (resource is null)
        {
            await WriteRemoteResponseAsync(stream, "Recurso nao encontrado.", "text/plain; charset=utf-8", token, 404);
            return;
        }

        await using var memory = new MemoryStream();
        await resource.Stream.CopyToAsync(memory, token);
        await WriteBinaryResponseAsync(stream, memory.ToArray(), contentType, token);
    }

    private static async Task WriteFileResponseAsync(RemoteHttpRequest request, NetworkStream stream, string filePath, string contentType, CancellationToken token, bool allowRanges)
    {
        string fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
        {
            await WriteRemoteResponseAsync(stream, "Arquivo nao encontrado.", "text/plain; charset=utf-8", token, 404);
            return;
        }

        var info = new FileInfo(fullPath);
        long start = 0;
        long end = info.Length - 1;
        int statusCode = 200;
        string extraHeaders = allowRanges ? "Accept-Ranges: bytes\r\n" : "";

        if (allowRanges && request.Headers.TryGetValue("range", out string? rangeHeader) && rangeHeader.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
        {
            string[] parts = rangeHeader[6..].Split('-', 2);
            if (long.TryParse(parts[0], out long parsedStart))
            {
                start = Math.Clamp(parsedStart, 0, Math.Max(0, info.Length - 1));
            }

            if (parts.Length == 2 && long.TryParse(parts[1], out long parsedEnd))
            {
                end = Math.Clamp(parsedEnd, start, Math.Max(0, info.Length - 1));
            }

            if (start > end || info.Length <= 0)
            {
                await WriteRemoteResponseAsync(stream, "", "text/plain", token, 416);
                return;
            }

            statusCode = 206;
            extraHeaders += $"Content-Range: bytes {start}-{end}/{info.Length}\r\n";
        }

        long bytesToSend = Math.Max(0, end - start + 1);
        string statusText = GetHttpStatusText(statusCode);
        string headers = $"HTTP/1.1 {statusCode} {statusText}\r\nContent-Type: {contentType}\r\nContent-Length: {bytesToSend}\r\n{extraHeaders}Connection: close\r\nCache-Control: no-store\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(headers), token);

        await using var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 128 * 1024, useAsync: true);
        file.Seek(start, SeekOrigin.Begin);
        var buffer = new byte[128 * 1024];
        long remaining = bytesToSend;
        while (remaining > 0)
        {
            int read = await file.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), token);
            if (read <= 0)
            {
                break;
            }

            await stream.WriteAsync(buffer.AsMemory(0, read), token);
            remaining -= read;
        }
    }

    private static async Task WriteBinaryResponseAsync(NetworkStream stream, byte[] bodyBytes, string contentType, CancellationToken token, int statusCode = 200)
    {
        string statusText = GetHttpStatusText(statusCode);
        string headers = $"HTTP/1.1 {statusCode} {statusText}\r\nContent-Type: {contentType}\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(headers), token);
        if (bodyBytes.Length > 0)
        {
            await stream.WriteAsync(bodyBytes, token);
        }
    }

    private static string GetContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            ".mp3" => "audio/mpeg",
            ".wav" => "audio/wav",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream"
        };
    }

    private static string GetHttpStatusText(int statusCode)
    {
        return statusCode switch
        {
            204 => "No Content",
            206 => "Partial Content",
            400 => "Bad Request",
            403 => "Forbidden",
            404 => "Not Found",
            409 => "Conflict",
            416 => "Range Not Satisfiable",
            500 => "Internal Server Error",
            _ => "OK"
        };
    }

    private string BuildRemotePage(bool authenticated, string message)
    {
        string pinJson = JsonSerializer.Serialize(PinBox.Text.Trim());
        string messageJson = JsonSerializer.Serialize(message ?? "");
        var html = new StringBuilder();
        html.Append("""
<!doctype html>
<html lang="pt-BR">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1,viewport-fit=cover">
<title>Som de Fundo Pro</title>
<style>
*{box-sizing:border-box}html,body{margin:0;max-width:100%;overflow-x:hidden}body{min-height:100vh;background:radial-gradient(circle at 18% 0,#0d2440 0,#08111d 43%,#030711 100%);color:#edf5ff;font-family:Inter,Segoe UI,Arial,sans-serif;padding:clamp(10px,3vw,18px);padding-bottom:28px}.app{width:min(100%,560px);margin:0 auto}.top{display:grid;grid-template-columns:minmax(0,1fr) minmax(138px,46%);align-items:center;gap:10px;margin-bottom:14px}.brand{display:flex;align-items:center;gap:10px;min-width:0}.logo{width:46px;height:46px;min-width:46px;border-radius:12px;object-fit:cover;background:#111827;box-shadow:0 0 0 1px #29445f}.logo:before{display:none!important}.brand h1{font-size:clamp(18px,5vw,22px);line-height:1.05;margin:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.brand i{color:#3b82f6;font-style:italic}select,input,button{font:inherit}.playlist{width:100%;min-width:0;height:44px;border-radius:14px;border:1px solid #324159;background:#121b2a;color:#fff;padding:0 10px;font-weight:800;font-size:clamp(12px,3.4vw,14px)}.card{background:linear-gradient(145deg,rgba(21,30,45,.95),rgba(9,14,24,.96));border:1px solid #273449;border-radius:18px;box-shadow:0 14px 44px rgba(0,0,0,.28);padding:clamp(12px,3.5vw,16px);margin-bottom:14px}.now{display:grid;grid-template-columns:clamp(72px,24vw,96px) minmax(0,1fr);gap:clamp(10px,4vw,16px);align-items:center}.cover{width:100%;aspect-ratio:1;border-radius:13px;background:#07111d;border:1px solid #24364f;object-fit:cover;display:block}.title{font-size:clamp(18px,5.4vw,23px);font-weight:900;margin:0 0 8px;line-height:1.12;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;overflow-wrap:anywhere}.meta{display:flex;gap:8px;align-items:center;color:#93a4b8;font-weight:800;min-width:0}.meta span{min-width:0;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.dot{width:10px;height:10px;min-width:10px;border-radius:50%;background:#64748b}.dot.on{background:#22c55e}.dot.pause{background:#f97316}.progress{width:100%;height:9px;border-radius:999px;background:#1f2937;margin:16px 0 8px;overflow:hidden}.bar{height:100%;width:0;background:linear-gradient(90deg,#2563eb,#22d3ee);border-radius:999px;transition:width .2s ease}.times{display:flex;justify-content:space-between;color:#9aa8ba;font-weight:800}.actions,.padgrid{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:10px}.btn{height:56px;border:0;border-radius:14px;color:#fff;font-weight:900;font-size:clamp(13px,3.8vw,16px);touch-action:manipulation;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.pauseBtn{background:linear-gradient(135deg,#f97316,#ea580c)}.stopBtn{background:linear-gradient(135deg,#ef4444,#dc2626)}.volume{display:grid;grid-template-columns:46px minmax(0,1fr) 70px;gap:12px;align-items:center}.volIcon{width:46px;height:46px;border-radius:50%;display:grid;place-items:center;background:#172233;border:1px solid #334155;font-weight:900;color:#38bdf8}.range{width:100%;accent-color:#22d3ee}.volValue{height:56px;border-radius:14px;border:1px solid #334155;background:#111827;display:grid;place-items:center;color:#38bdf8;font-size:clamp(19px,6vw,24px);font-weight:900}.padgrid{margin-top:10px}.pad{height:86px;min-width:0;border-radius:14px;border:1px solid var(--accent,#2b3a50);background:linear-gradient(145deg,#121b2a,#0e1725);color:#e5edf7;padding:10px;display:grid;grid-template-columns:34px minmax(0,1fr);grid-template-rows:1fr auto;gap:4px 8px;align-items:center;text-align:left;touch-action:manipulation}.pad .num{grid-row:1/3;font-size:clamp(24px,8vw,31px);font-weight:900;color:var(--accent,#38bdf8)}.padText{min-width:0;align-self:end}.pad .name{font-weight:900;font-size:clamp(13px,3.8vw,15px);line-height:1.12;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden;overflow-wrap:anywhere}.pad .sub{color:#8ea0b5;font-size:11px;margin-top:3px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.padActions{grid-column:2;display:flex;align-items:center;justify-content:space-between;gap:6px;min-width:0}.pad .action{font-size:11px;color:#38bdf8;font-weight:900;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}.preview{height:26px;min-width:58px;border:1px solid var(--accent,#38bdf8);border-radius:999px;background:#0b1220;color:#dbeafe;font-weight:900;font-size:11px;padding:0 8px}.preview.active{background:linear-gradient(135deg,#2563eb,#0ea5e9);border-color:#38bdf8}.pad.playing{border-color:var(--accent,#0ea5e9);box-shadow:0 0 0 1px var(--accent,#0ea5e9) inset;color:#e0f2fe}.pad.empty{border-style:dashed;border-color:var(--accent,#0891b2)}.pad.empty .action{color:#67e8f9}.msg{color:#fbbf24;font-weight:700;margin:10px 0}.remoteTopActions{display:flex;align-items:center;justify-content:space-between;gap:10px;margin:10px 2px 14px}.lockBtn{height:40px;border-radius:12px;border:1px solid #334155;background:#172033;color:#dbeafe;font-weight:900;padding:0 14px}.connection{font-size:12px;font-weight:900;white-space:nowrap}.connection.ok{color:#34d399}.connection.warn{color:#fbbf24}.connection.off{color:#fb7185}.lockScreen{position:fixed;inset:0;background:rgba(3,7,17,.96);display:none;z-index:30;place-items:center;padding:22px;text-align:center}.lockScreen.open{display:grid}.lockPanel{width:min(100%,360px);border:1px solid #334155;background:#101722;border-radius:20px;padding:22px;box-shadow:0 18px 70px rgba(0,0,0,.55)}.foot{display:flex;justify-content:space-between;gap:8px;color:#9fb0c5;font-size:12px;margin:14px 4px}.foot span{min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}.modal{position:fixed;inset:auto 10px 14px 10px;max-width:540px;margin:0 auto;background:#101722;border:1px solid #304056;border-radius:20px;padding:16px;box-shadow:0 -18px 70px rgba(0,0,0,.55);display:none;z-index:10}.modal.open{display:block}.modalHead{display:flex;justify-content:space-between;align-items:center;margin-bottom:12px}.close{width:42px;height:42px;border-radius:50%;background:#172233;color:#dbeafe;border:1px solid #334155}.filePick{display:grid;grid-template-columns:minmax(0,1fr) auto;gap:12px;align-items:center;border:1px solid #0891b2;border-radius:14px;padding:14px;color:#38bdf8;font-weight:900}.filePick input{display:none}.fileInfo{background:#172233;border:1px solid #26374d;border-radius:14px;padding:12px;margin:12px 0;color:#dbeafe;overflow-wrap:anywhere}.uploadBar{height:8px;background:#0b1020;border-radius:999px;overflow:hidden;margin-top:8px}.uploadBar span{display:block;height:100%;width:0;background:linear-gradient(90deg,#2563eb,#22d3ee)}.modalActions{display:grid;grid-template-columns:1fr 1fr;gap:12px}.secondary{background:#172033;border:1px solid #334155}.primary{background:linear-gradient(135deg,#2563eb,#0ea5e9)}@media(max-width:360px){body{padding:9px}.top{grid-template-columns:1fr;gap:8px}.brand h1{font-size:20px}.playlist{max-width:none}.now{grid-template-columns:68px minmax(0,1fr)}.volume{grid-template-columns:40px minmax(0,1fr) 58px;gap:8px}.volIcon{width:40px;height:40px}.volValue{height:50px}.padgrid{gap:8px}.pad{height:82px;padding:8px;grid-template-columns:28px minmax(0,1fr)}.preview{min-width:50px;padding:0 6px}.pad .action{font-size:10px}}
</style>
</head>
<body>
<div class="app">
""");
        if (!authenticated)
        {
            html.Append("""
<div class="top"><div class="brand"><img class="logo" src="/api/app-icon" alt=""><h1>Som de Fundo <i>Pro</i></h1></div></div>
<div class="card">
<h2>Conectar controle</h2>
<p style="color:#9aa8ba">Digite o PIN exibido no computador para acessar o controle remoto.</p>
<form method="get" action="/">
<input name="pin" inputmode="numeric" placeholder="Digite o PIN" style="width:100%;height:48px;border-radius:12px;border:1px solid #334155;background:#0b1020;color:#fff;padding:0 12px;margin:8px 0 12px">
<button class="btn primary" type="submit">Conectar</button>
</form>
</div></div></body></html>
""");
            return html.ToString();
        }

        html.Append("""
<div class="top">
  <div class="brand"><img class="logo" src="/api/app-icon" alt=""><h1>Som de Fundo <i>Pro</i></h1></div>
  <select id="playlist" class="playlist"></select>
</div>
<section class="card now">
  <img id="cover" class="cover" src="/api/app-icon" alt="">
  <div>
    <h2 id="current" class="title">Nenhum som ativo</h2>
    <div class="meta"><span id="dot" class="dot"></span><span id="status">Parado</span><span>|</span><span id="mode">Modo: Standby</span></div>
    <div class="progress"><div id="bar" class="bar"></div></div>
    <div class="times"><span id="pos">00:00</span><span id="dur">00:00</span></div>
  </div>
</section>
<section class="actions">
  <button id="pause" class="btn pauseBtn">Pausar / Retomar</button>
  <button id="stop" class="btn stopBtn">Parar</button>
</section>
<section class="card volume">
  <div class="volIcon">VOL</div>
  <div><div style="font-weight:800;margin-bottom:8px">VOLUME GERAL</div><input id="volume" class="range" type="range" min="0" max="100"></div>
  <div id="volValue" class="volValue">80%</div>
</section>
<section id="pads" class="padgrid"></section>
<div class="remoteTopActions"><button id="lockRemote" class="lockBtn">Bloquear tela</button><span id="connection" class="connection ok">Conectado</span></div>
<div class="foot"><span>Rede local</span><span id="updated">Atualizado agora</span></div>
</div>
<audio id="previewAudio" preload="none"></audio>
<div id="lockScreen" class="lockScreen"><div class="lockPanel"><h2>Controle bloqueado</h2><p style="color:#9aa8ba">O audio continua ativo. Desbloqueie para voltar aos controles.</p><button id="unlockRemote" class="btn primary">Desbloquear</button></div></div>
<div id="uploadModal" class="modal">
  <div class="modalHead"><h2 id="uploadTitle">Adicionar musica</h2><button id="closeUpload" class="close">X</button></div>
  <div class="filePick"><label for="fileInput">Selecionar arquivo</label><span>MP3, WAV, OGG<br><small>Max. 800 MB</small></span><input id="fileInput" type="file" accept=".mp3,.wav,.ogg,audio/mpeg,audio/wav,audio/ogg"></div>
  <div id="fileInfo" class="fileInfo">Nenhum arquivo selecionado.</div>
  <div class="uploadBar"><span id="uploadProgress"></span></div>
  <p id="uploadMsg" style="color:#9aa8ba">Arquivo sera copiado e salvo no Som de Fundo Pro.</p>
  <div class="modalActions"><button id="cancelUpload" class="btn secondary">Cancelar</button><button id="sendUpload" class="btn primary">Enviar para o botao</button></div>
</div>
<script>
const PIN=__PIN_JSON__;
const INITIAL_MESSAGE=__MESSAGE_JSON__;
let state=null, uploadPad=null, selectedFile=null, xhr=null, previewingPad=null, volumeTimer=null, volumeSent=0, volumeTarget=0, volumeDragging=false, failCount=0;
const qs=s=>document.querySelector(s);
const fmt=s=>{s=Math.max(0,Math.floor(s||0));return `${String(Math.floor(s/60)).padStart(2,'0')}:${String(s%60).padStart(2,'0')}`};
const withPin=path=>`${path}${path.includes('?')?'&':'?'}pin=${encodeURIComponent(PIN)}`;
const api=(path,opts={})=>fetch(withPin(path),opts).then(r=>r.json());
function setConnection(kind,text){const el=qs('#connection');if(!el)return;el.className='connection '+kind;el.textContent=text;}
async function loadState(){try{state=await api('/api/state');failCount=0;setConnection('ok','Conectado');render();}catch{failCount++;setConnection(failCount>=3?'off':'warn',failCount>=3?'Sem conexao':'Reconectando...');}}
function render(){
 if(!state||!state.ok)return;
 const playlist=qs('#playlist'); playlist.innerHTML='';
 state.playlists.forEach(p=>{const o=document.createElement('option');o.value=p.id;o.textContent='Playlist: '+p.name;playlist.appendChild(o);});
 playlist.value=state.playlistIndex;
 qs('#current').textContent=state.current||'Nenhum som ativo';
 const coverPath=state.currentCoverUrl||'/api/cover';
 qs('#cover').src=withPin(`${coverPath}${coverPath.includes('?')?'&':'?'}v=${state.currentPadId||0}`);
 qs('#status').textContent=state.status.toUpperCase();
 qs('#mode').textContent=state.statusText||'Modo: Standby';
 qs('#dot').className='dot '+(state.status==='Tocando'?'on':state.status==='Pausado'?'pause':'');
 qs('#bar').style.width=Math.max(0,Math.min(100,state.progress||0))+'%';
 qs('#pos').textContent=fmt(state.positionSeconds); qs('#dur').textContent=fmt(state.durationSeconds);
 if(!volumeDragging){qs('#volume').value=state.volume; qs('#volValue').textContent=state.volume+'%'; volumeSent=Number(state.volume)||0; volumeTarget=volumeSent;}
 renderPads();
 qs('#updated').textContent='Atualizado agora';
}
function renderPads(){
  const pads=qs('#pads'); pads.innerHTML='';
  state.pads.forEach(p=>{
   const b=document.createElement('div'); b.className='pad'+(p.isPlaying?' playing':'')+(!p.hasAudio?' empty':''); b.tabIndex=0; b.role='button'; b.style.setProperty('--accent',p.color||'#38bdf8');
   const previewLabel=previewingPad===p.id?'Parar':'Previa';
   b.innerHTML=`<span class="num">${p.id}</span><span class="padText"><span class="name">${escapeHtml(p.name)}</span><span class="sub">${p.hasAudio?(p.isPlaying?(state.isPaused?'PAUSADO':'TOCANDO'):'Pronto para tocar'):'Sem musica'}</span></span><span class="padActions"><span class="action">${p.hasAudio?'Tocar':'Adicionar musica'}</span>${p.hasAudio?`<button class="preview${previewingPad===p.id?' active':''}" type="button" data-pad="${p.id}">${previewLabel}</button>`:''}</span>`;
   b.onclick=()=>p.hasAudio?playPad(p.id):openUpload(p);
   b.onkeydown=e=>{if(e.key==='Enter'||e.key===' '){e.preventDefault();b.click();}};
   pads.appendChild(b);
  });
 document.querySelectorAll('.preview').forEach(btn=>btn.onclick=e=>{e.stopPropagation();previewPad(Number(btn.dataset.pad));});
}
function escapeHtml(v){return String(v).replace(/[&<>"']/g,m=>({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[m]));}
async function playPad(id){await fetch(`/play?id=${id}&pin=${encodeURIComponent(PIN)}`); await loadState();}
qs('#pause').onclick=async()=>{await fetch(`/pause?pin=${encodeURIComponent(PIN)}`); await loadState();}
qs('#stop').onclick=async()=>{await fetch(`/stop?pin=${encodeURIComponent(PIN)}`); await loadState();}
qs('#playlist').onchange=async e=>{await api('/api/select-playlist?index='+encodeURIComponent(e.target.value),{method:'POST'}); await loadState();}
qs('#lockRemote').onclick=()=>qs('#lockScreen').classList.add('open');
qs('#unlockRemote').onclick=()=>qs('#lockScreen').classList.remove('open');
function smoothVolumeTo(value){
 volumeTarget=Math.max(0,Math.min(100,Number(value)||0)); qs('#volValue').textContent=volumeTarget+'%';
 if(volumeTimer)return;
 volumeTimer=setInterval(()=>{const diff=volumeTarget-volumeSent;if(Math.abs(diff)<1){volumeSent=volumeTarget;clearInterval(volumeTimer);volumeTimer=null;}else{volumeSent+=Math.sign(diff)*Math.min(4,Math.abs(diff));} api('/api/volume?value='+encodeURIComponent(Math.round(volumeSent)),{method:'POST'}).catch(()=>{});},55);
}
qs('#volume').onpointerdown=()=>volumeDragging=true;
qs('#volume').onpointerup=()=>volumeDragging=false;
qs('#volume').oninput=e=>smoothVolumeTo(e.target.value);
function previewPad(id){
 const audio=qs('#previewAudio');
 if(previewingPad===id&&!audio.paused){audio.pause();audio.currentTime=0;previewingPad=null;renderPads();return;}
 previewingPad=id; renderPads(); audio.src=withPin(`/api/preview?playlist=${state.playlistIndex}&pad=${id}&t=${Date.now()}`);
 audio.play().catch(()=>{previewingPad=null;renderPads();alert('Nao foi possivel tocar a previa neste celular.');});
}
qs('#previewAudio').onended=()=>{previewingPad=null;renderPads();};
qs('#previewAudio').onpause=()=>{if(qs('#previewAudio').currentTime===0){previewingPad=null;renderPads();}};
function openUpload(p){uploadPad=p;selectedFile=null;qs('#uploadTitle').textContent='Adicionar musica ao Botao '+p.id;qs('#fileInput').value='';qs('#fileInfo').textContent='Playlist atual: '+state.playlists[state.playlistIndex].name;qs('#uploadProgress').style.width='0%';qs('#uploadMsg').textContent='Arquivo sera copiado e salvo no Som de Fundo Pro.';qs('#uploadModal').classList.add('open');}
function closeUpload(){if(xhr){xhr.abort();xhr=null;}qs('#uploadModal').classList.remove('open');}
qs('#closeUpload').onclick=closeUpload; qs('#cancelUpload').onclick=closeUpload;
qs('#fileInput').onchange=e=>{selectedFile=e.target.files[0]||null;if(!selectedFile)return;const mb=selectedFile.size/1024/1024;let msg=`${selectedFile.name} - ${mb.toFixed(1)} MB`;if(mb>800)msg+=' - acima do limite';else if(mb>40)msg+=' - envio pode demorar';qs('#fileInfo').textContent=msg;};
qs('#sendUpload').onclick=()=>{if(!selectedFile||!uploadPad)return alert('Selecione um arquivo.');const ext=selectedFile.name.split('.').pop().toLowerCase();if(!['mp3','wav','ogg'].includes(ext))return alert('Use MP3, WAV ou OGG.');if(selectedFile.size<=0||selectedFile.size>800*1024*1024)return alert('Arquivo vazio ou acima de 800 MB.');if(!confirm('Enviar '+selectedFile.name+' para o Botao '+uploadPad.id+'?'))return; xhr=new XMLHttpRequest();xhr.open('POST',`/api/upload?pin=${encodeURIComponent(PIN)}&playlist=${state.playlistIndex}&pad=${uploadPad.id}&name=${encodeURIComponent(selectedFile.name)}`);xhr.setRequestHeader('Content-Type',selectedFile.type||({'mp3':'audio/mpeg','wav':'audio/wav','ogg':'audio/ogg'}[ext]));xhr.upload.onprogress=e=>{if(e.lengthComputable){const pct=Math.round(e.loaded*100/e.total);qs('#uploadProgress').style.width=pct+'%';qs('#uploadMsg').textContent='Enviando... '+pct+'%';}};xhr.onload=async()=>{let res={ok:false,error:'Erro no envio'};try{res=JSON.parse(xhr.responseText)}catch{} if(res.ok){qs('#uploadProgress').style.width='100%';qs('#uploadMsg').textContent='Musica adicionada ao botao.';await loadState();setTimeout(closeUpload,700);}else{qs('#uploadMsg').textContent=res.error||'Erro no envio';}};xhr.onerror=()=>qs('#uploadMsg').textContent='Falha de conexao durante o envio.';xhr.onabort=()=>qs('#uploadMsg').textContent='Envio cancelado.';xhr.send(selectedFile);};
if(INITIAL_MESSAGE) console.log(INITIAL_MESSAGE);
loadState(); setInterval(loadState,2500);
</script>
</body></html>
""".Replace("__PIN_JSON__", pinJson).Replace("__MESSAGE_JSON__", messageJson));
        return html.ToString();
    }

    private void RefreshRemotePanel()
    {
        if (RemoteUrlText is null)
        {
            return;
        }

        string baseUrl = GetRemoteBaseUrl();
        RemoteUrlText.Text = baseUrl;
        RemotePinText.Text = $"PIN: {PinBox.Text}";
        RemoteStatusText.Text = $"{(_remoteServer is null ? "Servidor desligado" : "Servidor ligado")}  -  Conectados: {_remoteConnections}";
        RemoteQrImage.Source = CreateQrImage(GetRemoteAccessUrl());
    }

    private string GetRemoteBaseUrl()
    {
        return $"http://{GetLocalIpAddress()}:{RemotePort}";
    }

    private string GetRemoteAccessUrl()
    {
        return $"{GetRemoteBaseUrl()}/?pin={WebUtility.UrlEncode(PinBox.Text.Trim())}";
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 80);
            if (socket.LocalEndPoint is IPEndPoint endpoint)
            {
                return endpoint.Address.ToString();
            }
        }
        catch
        {
            // Fallback abaixo.
        }

        return Dns.GetHostEntry(Dns.GetHostName())
            .AddressList
            .FirstOrDefault(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            ?.ToString() ?? "127.0.0.1";
    }

    private static bool ValidateRemotePin(Uri uri, string expectedPin)
    {
        return string.Equals(GetQueryValue(uri, "pin"), expectedPin, StringComparison.Ordinal);
    }

    private static string GetQueryValue(Uri uri, string key)
    {
        string query = uri.Query.TrimStart('?');
        foreach (string pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] parts = pair.Split('=', 2);
            if (parts.Length > 0 && string.Equals(WebUtility.UrlDecode(parts[0]), key, StringComparison.OrdinalIgnoreCase))
            {
                return parts.Length == 2 ? WebUtility.UrlDecode(parts[1]) : "";
            }
        }

        return "";
    }

    private static async Task WriteRemoteResponseAsync(NetworkStream stream, string body, string contentType, CancellationToken token, int statusCode = 200)
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        string statusText = GetHttpStatusText(statusCode);
        string headers = $"HTTP/1.1 {statusCode} {statusText}\r\nContent-Type: {contentType}\r\nContent-Length: {bodyBytes.Length}\r\nConnection: close\r\nCache-Control: no-store\r\n\r\n";
        await stream.WriteAsync(Encoding.UTF8.GetBytes(headers), token);
        if (bodyBytes.Length > 0)
        {
            await stream.WriteAsync(bodyBytes, token);
        }
    }

    private static BitmapImage CreateQrImage(string text)
    {
        using var generator = new QRCodeGenerator();
        using QRCodeData data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new PngByteQRCode(data);
        byte[] bytes = qrCode.GetGraphic(18);

        var image = new BitmapImage();
        image.BeginInit();
        image.StreamSource = new MemoryStream(bytes);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        ShowModal(AboutOverlay);
    }

    private void CloseAbout_Click(object sender, RoutedEventArgs e)
    {
        HideModals();
    }

    private void OpenInstagram_Click(object sender, RoutedEventArgs e)
    {
        OpenExternalUrl("https://www.instagram.com/alan.psxd1");
    }

    private void OpenWhatsappGroup_Click(object sender, RoutedEventArgs e)
    {
        OpenExternalUrl("https://chat.whatsapp.com/BKkRZhcOjjgFWc6kxTQQoB");
    }

    private static void OpenExternalUrl(string url)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }

    private void NewPlaylist_Click(object sender, RoutedEventArgs e)
    {
        string? name = PromptForPlaylistName("Nova playlist", "Nome da playlist:", CreateUniquePlaylistName("Nova Playlist"));
        if (name is null)
        {
            return;
        }

        CreateAutomaticBackup("nova-playlist");
        CaptureCurrentPlaylist();
        _playlists.Add(CreateDefaultPlaylistState(name));
        _currentPlaylistIndex = _playlists.Count - 1;
        ApplyCurrentPlaylist();
        SaveLocalSettings();
    }

    private void DuplicatePlaylist_Click(object sender, RoutedEventArgs e)
    {
        CreateAutomaticBackup("duplicar-playlist");
        CaptureCurrentPlaylist();
        PlaylistState current = _playlists[_currentPlaylistIndex];
        string name = CreateUniquePlaylistName($"{current.Name} - Copia");
        _playlists.Add(current.Clone(name));
        _currentPlaylistIndex = _playlists.Count - 1;
        ApplyCurrentPlaylist();
        SaveLocalSettings();
    }

    private void DeletePlaylist_Click(object sender, RoutedEventArgs e)
    {
        if (_playlists.Count == 0)
        {
            InitializePlaylists();
        }

        string playlistName = _playlists[_currentPlaylistIndex].Name;
        if (MessageBox.Show(this, $"Excluir a playlist '{playlistName}'?", "Excluir playlist", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        CreateAutomaticBackup("excluir-playlist");
        StopPlayback(useFade: false);
        if (_playlists.Count == 1)
        {
            _playlists[0] = CreateDefaultPlaylistState(DefaultPlaylistName);
            _currentPlaylistIndex = 0;
        }
        else
        {
            _playlists.RemoveAt(_currentPlaylistIndex);
            _currentPlaylistIndex = Math.Clamp(_currentPlaylistIndex, 0, _playlists.Count - 1);
        }

        ApplyCurrentPlaylist();
        SaveLocalSettings();
    }

    private void EditPlaylist_Click(object sender, RoutedEventArgs e)
    {
        CaptureCurrentPlaylist();
        string currentName = _playlists[_currentPlaylistIndex].Name;
        string? name = PromptForPlaylistName("Editar playlist", "Nome da playlist:", currentName);
        if (name is null)
        {
            return;
        }

        CreateAutomaticBackup("editar-playlist");
        _playlists[_currentPlaylistIndex] = _playlists[_currentPlaylistIndex].Clone(name);
        UpdatePlaylistTitle();
        SaveLocalSettings();
    }

    private void PlaylistSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingPlaylistSelector || PlaylistSelector.SelectedIndex < 0 || PlaylistSelector.SelectedIndex == _currentPlaylistIndex)
        {
            return;
        }

        CaptureCurrentPlaylist();
        StopPlayback(useFade: false);
        _currentPlaylistIndex = PlaylistSelector.SelectedIndex;
        ApplyCurrentPlaylist();
        SaveLocalSettings();
    }

    private void InitializePlaylists()
    {
        _playlists.Clear();
        string name = string.IsNullOrWhiteSpace(PlaylistNameText.Text) ? DefaultPlaylistName : PlaylistNameText.Text.Trim();
        _playlists.Add(CapturePlaylistState(name));
        _currentPlaylistIndex = 0;
        UpdatePlaylistTitle();
    }

    private void CaptureCurrentPlaylist()
    {
        if (_playlists.Count == 0)
        {
            InitializePlaylists();
            return;
        }

        _playlists[_currentPlaylistIndex] = CapturePlaylistState(_playlists[_currentPlaylistIndex].Name);
    }

    private PlaylistState CapturePlaylistState(string name)
    {
        return new PlaylistState(name, _pads.Select(PadBackup.FromPad).ToList());
    }

    private PlaylistState CreateDefaultPlaylistState(string name)
    {
        var pads = new List<PadBackup>();
        for (int i = 1; i <= 20; i++)
        {
            pads.Add(new PadBackup
            {
                Id = i,
                Name = $"Botao {i}",
                Color = DefaultPadColors[i - 1],
                SoundPath = null,
                CoverPath = null,
                Volume = i == 1 ? 0 : 100,
                Loop = false,
                OriginalSoundPath = null,
                InstrumentalPath = null,
                VocalPath = null
            });
        }

        return new PlaylistState(name, pads);
    }

    private void ApplyCurrentPlaylist()
    {
        PlaylistState playlist = _playlists[_currentPlaylistIndex];
        foreach (PadCard pad in _pads)
        {
            PadBackup? backup = playlist.Pads.FirstOrDefault(item => item.Id == pad.Id);
            if (backup is not null)
            {
                backup.ApplyTo(pad);
            }
        }

        RefreshAllPadAudioStatus();
        UpdatePlaylistTitle();
    }

    private void RefreshAllPadAudioStatus()
    {
        foreach (PadCard pad in _pads)
        {
            pad.RefreshAudioStatus();
        }
    }

    private void UpdatePlaylistTitle()
    {
        if (PlaylistNameText is null || _playlists.Count == 0)
        {
            return;
        }

        PlaylistNameText.Text = _playlists[_currentPlaylistIndex].Name;
        RefreshPlaylistSelector();
    }

    private void RefreshPlaylistSelector()
    {
        if (PlaylistSelector is null)
        {
            return;
        }

        _updatingPlaylistSelector = true;
        PlaylistSelector.Items.Clear();
        foreach (PlaylistState playlist in _playlists)
        {
            PlaylistSelector.Items.Add(playlist.Name);
        }

        PlaylistSelector.SelectedIndex = Math.Clamp(_currentPlaylistIndex, 0, Math.Max(0, _playlists.Count - 1));
        _updatingPlaylistSelector = false;
    }

    private string CreateUniquePlaylistName(string baseName)
    {
        string cleanBaseName = string.IsNullOrWhiteSpace(baseName) ? DefaultPlaylistName : baseName.Trim();
        string candidate = cleanBaseName;
        int suffix = 2;
        while (_playlists.Any(item => item.Name.Equals(candidate, StringComparison.OrdinalIgnoreCase)))
        {
            candidate = $"{cleanBaseName} {suffix}";
            suffix++;
        }

        return candidate;
    }

    private string? PromptForPlaylistName(string title, string label, string initialValue)
    {
        var dialog = new Window
        {
            Title = title,
            Owner = this,
            Width = 360,
            Height = 180,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = new SolidColorBrush(Color.FromRgb(21, 26, 35))
        };

        var nameBox = new TextBox
        {
            Text = initialValue,
            Margin = new Thickness(0, 8, 0, 16)
        };

        var okButton = new Button
        {
            Content = "OK",
            Style = (Style)FindResource("PrimaryButton"),
            Width = 86,
            Margin = new Thickness(0, 0, 8, 0),
            IsDefault = true
        };
        var cancelButton = new Button
        {
            Content = "Cancelar",
            Style = (Style)FindResource("RoundedButton"),
            Width = 100,
            IsCancel = true
        };

        okButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(nameBox.Text))
            {
                MessageBox.Show(dialog, "Informe um nome para a playlist.", title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            dialog.DialogResult = true;
        };

        var footer = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        footer.Children.Add(okButton);
        footer.Children.Add(cancelButton);

        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = label, Foreground = (Brush)FindResource("TextBrush"), FontWeight = FontWeights.SemiBold });
        panel.Children.Add(nameBox);
        panel.Children.Add(footer);

        dialog.Content = panel;
        nameBox.SelectAll();
        nameBox.Focus();

        return dialog.ShowDialog() == true ? nameBox.Text.Trim() : null;
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (ModalShade.Visibility == Visibility.Visible)
            {
                HideModals();
            }
            else
            {
                StopPlayback();
            }
        }
        else if (e.Key == Key.Space)
        {
            PauseResume_Click(this, new RoutedEventArgs());
        }
        else
        {
            int? padNumber = e.Key switch
            {
                Key.D1 or Key.NumPad1 => 1,
                Key.D2 or Key.NumPad2 => 2,
                Key.D3 or Key.NumPad3 => 3,
                Key.D4 or Key.NumPad4 => 4,
                Key.D5 or Key.NumPad5 => 5,
                Key.D6 or Key.NumPad6 => 6,
                Key.D7 or Key.NumPad7 => 7,
                Key.D8 or Key.NumPad8 => 8,
                Key.D9 or Key.NumPad9 => 9,
                Key.D0 or Key.NumPad0 => 10,
                _ => null
            };

            if (padNumber is int id)
            {
                PlayPadById(id);
            }
        }
    }

    private void PlayPadById(int id)
    {
        PadCard? pad = _pads.FirstOrDefault(p => p.Id == id);
        if (pad is null)
        {
            return;
        }

        Pad_Click(new Button { Tag = pad }, new RoutedEventArgs());
    }

    private void ShowModal(UIElement overlay)
    {
        ModalShade.Visibility = Visibility.Visible;
        AboutOverlay.Visibility = Visibility.Collapsed;
        EditorOverlay.Visibility = Visibility.Collapsed;
        AiOverlay.Visibility = Visibility.Collapsed;
        EqualizerOverlay.Visibility = Visibility.Collapsed;
        RemoteOverlay.Visibility = Visibility.Collapsed;
        SettingsOverlay.Visibility = Visibility.Collapsed;
        LibraryOverlay.Visibility = Visibility.Collapsed;
        FirstRunOverlay.Visibility = Visibility.Collapsed;
        SetEqualizerButtonHighlighted(overlay == EqualizerOverlay);
        SetRemoteButtonHighlighted(overlay == RemoteOverlay);
        overlay.Visibility = Visibility.Visible;
    }

    private void HideModals()
    {
        ModalShade.Visibility = Visibility.Collapsed;
        AboutOverlay.Visibility = Visibility.Collapsed;
        EditorOverlay.Visibility = Visibility.Collapsed;
        AiOverlay.Visibility = Visibility.Collapsed;
        EqualizerOverlay.Visibility = Visibility.Collapsed;
        RemoteOverlay.Visibility = Visibility.Collapsed;
        SettingsOverlay.Visibility = Visibility.Collapsed;
        LibraryOverlay.Visibility = Visibility.Collapsed;
        FirstRunOverlay.Visibility = Visibility.Collapsed;
        SetEqualizerButtonHighlighted(false);
        SetRemoteButtonHighlighted(false);
    }

    protected override void OnClosed(EventArgs e)
    {
        SaveLocalSettings();
        _timer.Stop();
        CancelActiveFade();
        _suppressPlaybackStopped = true;
        _aiCancellation?.Cancel();
        StopRemoteServer();
        _deckA.Dispose();
        _deckB.Dispose();
        _previewDeck.Dispose();
        _mainOutput.Stop();
        _mainOutput.Dispose();
        base.OnClosed(e);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

public sealed class PadCard : NotifyModel
{
    private string _name = "";
    private string _color = "#2563EB";
    private string _soundLabel = "Sem audio";
    private string? _soundPath;
    private bool _hasMissingAudio;
    private string? _coverPath;
    private double _volume = 100;
    private bool _loop;
    private bool _isPlaying;
    private string? _originalSoundPath;
    private string? _instrumentalPath;
    private string? _vocalPath;

    public int Id { get; init; }
    public string NumberLabel => $"[{Id}]";

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Color
    {
        get => _color;
        set
        {
            if (SetField(ref _color, value))
            {
                OnPropertyChanged(nameof(ColorBrush));
            }
        }
    }

    public Brush ColorBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(Color));

    public string? SoundPath
    {
        get => _soundPath;
        set
        {
            if (SetField(ref _soundPath, value))
            {
                SoundLabel = string.IsNullOrWhiteSpace(value)
                    ? "Sem audio"
                    : value.StartsWith("offline://", StringComparison.OrdinalIgnoreCase)
                        ? "Offline simulado"
                        : Path.GetFileName(value);
                RefreshAudioStatus();
            }
        }
    }

    public string SoundLabel
    {
        get => _soundLabel;
        set => SetField(ref _soundLabel, value);
    }

    public bool HasMissingAudio
    {
        get => _hasMissingAudio;
        private set
        {
            if (SetField(ref _hasMissingAudio, value))
            {
                OnPropertyChanged(nameof(AudioStatusText));
                OnPropertyChanged(nameof(AudioStatusBrush));
            }
        }
    }

    public string AudioStatusText => HasMissingAudio ? "Arquivo nao encontrado" : SoundLabel;

    public Brush AudioStatusBrush => HasMissingAudio
        ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(251, 113, 133))
        : new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 149, 167));

    public void RefreshAudioStatus()
    {
        HasMissingAudio = !string.IsNullOrWhiteSpace(SoundPath)
            && !SoundPath.StartsWith("offline://", StringComparison.OrdinalIgnoreCase)
            && !File.Exists(SoundPath);
        OnPropertyChanged(nameof(AudioStatusText));
        OnPropertyChanged(nameof(AudioStatusBrush));
    }

    public string? CoverPath
    {
        get => _coverPath;
        set
        {
            string? normalized = CoverImageService.NormalizeStoredCoverPath(value);
            if (SetField(ref _coverPath, normalized))
            {
                OnPropertyChanged(nameof(CoverLabel));
                OnPropertyChanged(nameof(CoverImageSource));
            }
        }
    }

    public string CoverLabel => CoverPath is null ? "SEM" : "CAPA";
    public ImageSource CoverImageSource => CoverImageService.LoadCoverImage(CoverPath);

    public double Volume
    {
        get => _volume;
        set
        {
            if (SetField(ref _volume, Math.Clamp(value, 0, 100)))
            {
                OnPropertyChanged(nameof(VolumeLabel));
            }
        }
    }

    public string VolumeLabel => $"{(int)Math.Round(Volume)}%";

    public bool Loop
    {
        get => _loop;
        set
        {
            if (SetField(ref _loop, value))
            {
                OnPropertyChanged(nameof(LoopLabel));
            }
        }
    }

    public string LoopLabel => Loop ? "LOOP" : "1X";

    public bool IsPlaying
    {
        get => _isPlaying;
        set => SetField(ref _isPlaying, value);
    }

    public string? OriginalSoundPath
    {
        get => _originalSoundPath;
        set => SetField(ref _originalSoundPath, value);
    }

    public string? InstrumentalPath
    {
        get => _instrumentalPath;
        set => SetField(ref _instrumentalPath, value);
    }

    public string? VocalPath
    {
        get => _vocalPath;
        set => SetField(ref _vocalPath, value);
    }
}

public sealed class OnlineAudio : NotifyModel
{
    private int _rank;
    private bool _isDownloaded;
    private bool _hasVoted;
    private string _downloadStatus = "Disponivel";

    private OnlineAudio(CommunityMusic source, bool hasVoted)
    {
        Source = source;
        HasVoted = hasVoted;
        DownloadStatus = hasVoted ? "Voce ja votou" : "Disponivel";
    }

    public CommunityMusic Source { get; }
    public string Id => Source.Id;
    public string Name => Source.Title;
    public string Artist => Source.Artist;
    public string UploadedBy => string.IsNullOrWhiteSpace(Source.UploadedBy) ? "Anonimo" : Source.UploadedBy;
    public string UploadedByLabel => $"Enviado por: {UploadedBy}";
    public string ArtistUploaderLabel => $"{Artist} - Enviado por: {UploadedBy}";
    public string Category => Source.Artist;
    public string StoragePath => Source.StoragePath;
    public TimeSpan Duration => TimeSpan.FromSeconds(Math.Max(0, Source.DurationSeconds));
    public DateTime CreatedAt => Source.CreatedAt.LocalDateTime;
    public string? DownloadedPath { get; set; }
    public string DurationLabel => FormatDuration(Duration);
    public string SizeLabel => FormatBytes(Source.SizeBytes);
    public int Votes => Source.Votes;
    public int Downloads => Source.Downloads;
    public bool IsApproved => Source.Approved;
    public string ApprovalLabel => Source.Approved ? "Aprovada" : "Votacao";
    public int Score => Source.Votes;

    public static OnlineAudio FromCommunity(CommunityMusic music, bool hasVoted) => new(music, hasVoted);

    public int Rank
    {
        get => _rank;
        set => SetField(ref _rank, value);
    }

    public bool IsDownloaded
    {
        get => _isDownloaded;
        set => SetField(ref _isDownloaded, value);
    }

    public bool HasVoted
    {
        get => _hasVoted;
        set
        {
            if (SetField(ref _hasVoted, value))
            {
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public string DownloadStatus
    {
        get => _downloadStatus;
        set
        {
            if (SetField(ref _downloadStatus, value))
            {
                OnPropertyChanged(nameof(StatusBrush));
            }
        }
    }

    public Brush StatusBrush
    {
        get
        {
            if (HasVoted || DownloadStatus.Contains("voto", StringComparison.OrdinalIgnoreCase) || DownloadStatus.Contains("votou", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Color.FromRgb(20, 83, 45));
            }

            if (DownloadStatus.Contains("baix", StringComparison.OrdinalIgnoreCase))
            {
                return new SolidColorBrush(Color.FromRgb(30, 64, 175));
            }

            return new SolidColorBrush(Color.FromRgb(30, 41, 59));
        }
    }

    public bool Matches(string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || Name.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Artist.Contains(query, StringComparison.OrdinalIgnoreCase)
            || UploadedBy.Contains(query, StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatDuration(TimeSpan value)
    {
        return $"{(int)value.TotalMinutes:00}:{value.Seconds:00}";
    }

    private static string FormatBytes(long value)
    {
        return value >= 1024 * 1024
            ? $"{value / 1024d / 1024d:0.0} MB"
            : $"{Math.Max(1, value / 1024)} KB";
    }
}

public sealed class BackupData
{
    public string? Pin { get; set; }
    public bool FirstRunCompleted { get; set; }
    public int GridColumns { get; set; }
    public double ScalePercent { get; set; }
    public int CrossfadeSeconds { get; set; }
    public bool EqEnabled { get; set; }
    public double EqBass { get; set; }
    public double EqMid { get; set; }
    public double EqTreble { get; set; }
    public string EqPreset { get; set; } = "padrao";
    public string? CommunitySenderName { get; set; }
    public int CurrentPlaylistIndex { get; set; }
    public List<PlaylistBackup> Playlists { get; set; } = [];
    public List<PadBackup> Pads { get; set; } = [];
}

public sealed class PlaylistState(string name, List<PadBackup> pads)
{
    public string Name { get; } = name;
    public List<PadBackup> Pads { get; } = pads;

    public PlaylistState Clone(string name)
    {
        return new PlaylistState(name, Pads.Select(ClonePad).ToList());
    }

    private static PadBackup ClonePad(PadBackup source)
    {
        return new PadBackup
        {
            Id = source.Id,
            Name = source.Name,
            Color = source.Color,
            SoundPath = source.SoundPath,
            CoverPath = source.CoverPath,
            Volume = source.Volume,
            Loop = source.Loop,
            OriginalSoundPath = source.OriginalSoundPath,
            InstrumentalPath = source.InstrumentalPath,
            VocalPath = source.VocalPath
        };
    }
}

public sealed class PlaylistBackup
{
    public string Name { get; set; } = "";
    public List<PadBackup> Pads { get; set; } = [];

    public static PlaylistBackup FromState(PlaylistState state)
    {
        return new PlaylistBackup
        {
            Name = state.Name,
            Pads = state.Pads.Select(ClonePad).ToList()
        };
    }

    public static PadBackup ClonePad(PadBackup source)
    {
        return new PadBackup
        {
            Id = source.Id,
            Name = source.Name,
            Color = source.Color,
            SoundPath = source.SoundPath,
            CoverPath = source.CoverPath,
            Volume = source.Volume,
            Loop = source.Loop,
            OriginalSoundPath = source.OriginalSoundPath,
            InstrumentalPath = source.InstrumentalPath,
            VocalPath = source.VocalPath
        };
    }
}

internal sealed record RemoteHttpRequest(
    string Method,
    string RawTarget,
    Dictionary<string, string> Headers,
    byte[] BodyPrefix,
    long ContentLength);

internal sealed record RemoteUploadTarget(string PlaylistName, string PadName);

internal sealed record RemoteUploadStatus(string Status, int Percent, string Message);

public sealed class PadBackup
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Color { get; set; } = "#2563EB";
    public string? SoundPath { get; set; }
    public string? CoverPath { get; set; }
    public double Volume { get; set; }
    public bool Loop { get; set; }
    public string? OriginalSoundPath { get; set; }
    public string? InstrumentalPath { get; set; }
    public string? VocalPath { get; set; }

    public static PadBackup FromPad(PadCard pad)
    {
        return new PadBackup
        {
            Id = pad.Id,
            Name = pad.Name,
            Color = pad.Color,
            SoundPath = pad.SoundPath,
            CoverPath = pad.CoverPath,
            Volume = pad.Volume,
            Loop = pad.Loop,
            OriginalSoundPath = pad.OriginalSoundPath,
            InstrumentalPath = pad.InstrumentalPath,
            VocalPath = pad.VocalPath
        };
    }

    public void ApplyTo(PadCard pad)
    {
        pad.Name = Name;
        pad.Color = Color;
        pad.SoundPath = SoundPath;
        pad.CoverPath = CoverPath;
        pad.Volume = Volume;
        pad.Loop = Loop;
        pad.OriginalSoundPath = OriginalSoundPath ?? SoundPath;
        pad.InstrumentalPath = InstrumentalPath;
        pad.VocalPath = VocalPath;
    }
}

public sealed class AudioDeck : IDisposable
{
    private WaveOutEvent? _output;
    private AudioFileReader? _reader;
    private EqualizerSampleProvider? _equalizerProvider;
    private DeckPlaybackSampleProvider? _playbackProvider;
    private VolumeSampleProvider? _volumeProvider;
    private IWaveProvider? _waveProvider;
    private MixingSampleProvider? _sharedMixer;

    public event EventHandler? PlaybackStopped;

    public bool HasAudio => _reader is not null;
    public TimeSpan Duration => _reader?.TotalTime ?? TimeSpan.Zero;

    public TimeSpan Position
    {
        get => _reader?.CurrentTime ?? TimeSpan.Zero;
        set
        {
            if (_reader is not null)
            {
                _reader.CurrentTime = value;
                _playbackProvider?.ResetEndState();
            }
        }
    }

    public double Volume
    {
        get => _volumeProvider?.Volume ?? 0;
        set
        {
            if (_volumeProvider is not null)
            {
                _volumeProvider.Volume = (float)Math.Clamp(value, 0, 1);
            }
        }
    }

    public void Load(string path, bool eqEnabled, double eqBass, double eqMid, double eqTreble)
    {
        Close();

        _reader = new AudioFileReader(path);
        _output = new WaveOutEvent();
        _equalizerProvider = new EqualizerSampleProvider(_reader, eqEnabled, eqBass, eqMid, eqTreble);
        _volumeProvider = new VolumeSampleProvider(_equalizerProvider);
        _waveProvider = new SampleToWaveProvider(_volumeProvider);
        _output.Init(_waveProvider);
        _output.PlaybackStopped += Output_PlaybackStopped;
    }

    public void Load(string path, bool eqEnabled, double eqBass, double eqMid, double eqTreble, MixingSampleProvider mixer)
    {
        Close();

        _reader = new AudioFileReader(path);
        _equalizerProvider = new EqualizerSampleProvider(_reader, eqEnabled, eqBass, eqMid, eqTreble);
        ISampleProvider mixerReadyProvider = EnsureMixerFormat(_equalizerProvider, mixer.WaveFormat);
        _playbackProvider = new DeckPlaybackSampleProvider(mixerReadyProvider, () => PlaybackStopped?.Invoke(this, EventArgs.Empty));
        _volumeProvider = new VolumeSampleProvider(_playbackProvider);
        EnsureCompatibleWaveFormat(_volumeProvider.WaveFormat, mixer.WaveFormat);
        _sharedMixer = mixer;
        _sharedMixer.AddMixerInput(_volumeProvider);
    }

    public void UpdateEqualizer(bool enabled, double bassDb, double midDb, double trebleDb)
    {
        _equalizerProvider?.Update(enabled, bassDb, midDb, trebleDb);
    }

    public void Play()
    {
        if (_playbackProvider is not null)
        {
            _playbackProvider.Play();
            return;
        }

        _output?.Play();
    }

    public void Pause()
    {
        if (_playbackProvider is not null)
        {
            _playbackProvider.Pause();
            return;
        }

        _output?.Pause();
    }

    public void Stop()
    {
        _output?.Stop();
    }

    public void Close()
    {
        if (_sharedMixer is not null && _volumeProvider is not null)
        {
            _sharedMixer.RemoveMixerInput(_volumeProvider);
            _sharedMixer = null;
        }

        if (_output is not null)
        {
            _output.PlaybackStopped -= Output_PlaybackStopped;
            _output.Stop();
            _output.Dispose();
            _output = null;
        }

        _reader?.Dispose();
        _reader = null;
        _equalizerProvider = null;
        _playbackProvider = null;
        _volumeProvider = null;
        _waveProvider = null;
    }

    public void Dispose()
    {
        Close();
    }

    private void Output_PlaybackStopped(object? sender, StoppedEventArgs e)
    {
        PlaybackStopped?.Invoke(this, EventArgs.Empty);
    }

    private static ISampleProvider EnsureMixerFormat(ISampleProvider source, WaveFormat targetFormat)
    {
        ISampleProvider provider = source;

        if (provider.WaveFormat.Channels == 1 && targetFormat.Channels == 2)
        {
            provider = new MonoToStereoSampleProvider(provider);
        }
        else if (provider.WaveFormat.Channels == 2 && targetFormat.Channels == 1)
        {
            provider = new StereoToMonoSampleProvider(provider);
        }
        else if (provider.WaveFormat.Channels != targetFormat.Channels)
        {
            throw new InvalidOperationException("Formato de audio incompativel com o mixer.");
        }

        if (provider.WaveFormat.SampleRate != targetFormat.SampleRate)
        {
            provider = new WdlResamplingSampleProvider(provider, targetFormat.SampleRate);
        }

        EnsureCompatibleWaveFormat(provider.WaveFormat, targetFormat);
        return provider;
    }

    private static void EnsureCompatibleWaveFormat(WaveFormat sourceFormat, WaveFormat targetFormat)
    {
        if (sourceFormat.SampleRate != targetFormat.SampleRate
            || sourceFormat.Channels != targetFormat.Channels
            || sourceFormat.Encoding != targetFormat.Encoding)
        {
            throw new InvalidOperationException("Formato de audio incompativel com o mixer.");
        }
    }
}

public sealed class DeckPlaybackSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly Action _ended;
    private readonly object _sync = new();
    private bool _paused = true;
    private bool _endedRaised;

    public DeckPlaybackSampleProvider(ISampleProvider source, Action ended)
    {
        _source = source;
        _ended = ended;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        bool paused;
        lock (_sync)
        {
            paused = _paused;
        }

        if (paused)
        {
            Array.Clear(buffer, offset, count);
            return count;
        }

        int read = _source.Read(buffer, offset, count);
        if (read < count)
        {
            Array.Clear(buffer, offset + read, count - read);
        }

        if (read == 0)
        {
            bool shouldRaise;
            lock (_sync)
            {
                shouldRaise = !_endedRaised;
                _endedRaised = true;
            }

            if (shouldRaise)
            {
                _ended();
            }
        }

        return count;
    }

    public void Play()
    {
        lock (_sync)
        {
            _paused = false;
        }
    }

    public void Pause()
    {
        lock (_sync)
        {
            _paused = true;
        }
    }

    public void ResetEndState()
    {
        lock (_sync)
        {
            _endedRaised = false;
        }
    }
}

public sealed class EqualizerSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly BiQuadFilter[,] _filters;
    private readonly object _sync = new();
    private bool _enabled;
    private double _bassDb;
    private double _midDb;
    private double _trebleDb;

    public EqualizerSampleProvider(ISampleProvider source, bool enabled, double bassDb, double midDb, double trebleDb)
    {
        _source = source;
        WaveFormat = source.WaveFormat;
        int channels = Math.Max(1, WaveFormat.Channels);
        _filters = new BiQuadFilter[channels, 3];
        Update(enabled, bassDb, midDb, trebleDb);
    }

    public WaveFormat WaveFormat { get; }

    public void Update(bool enabled, double bassDb, double midDb, double trebleDb)
    {
        lock (_sync)
        {
            _enabled = enabled;
            _bassDb = Math.Clamp(bassDb, -12, 12);
            _midDb = Math.Clamp(midDb, -12, 12);
            _trebleDb = Math.Clamp(trebleDb, -12, 12);

            for (int channel = 0; channel < WaveFormat.Channels; channel++)
            {
                _filters[channel, 0] = BiQuadFilter.LowShelf(WaveFormat.SampleRate, 100, 0.8f, (float)_bassDb);
                _filters[channel, 1] = BiQuadFilter.PeakingEQ(WaveFormat.SampleRate, 1000, 1.0f, (float)_midDb);
                _filters[channel, 2] = BiQuadFilter.HighShelf(WaveFormat.SampleRate, 8000, 0.8f, (float)_trebleDb);
            }
        }
    }

    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = _source.Read(buffer, offset, count);
        if (!_enabled)
        {
            return samplesRead;
        }

        int channels = WaveFormat.Channels;
        float trim = GetTrimFactor();
        for (int n = 0; n < samplesRead; n++)
        {
            int channel = n % channels;
            float sample = buffer[offset + n];
            lock (_sync)
            {
                sample = _filters[channel, 0].Transform(sample);
                sample = _filters[channel, 1].Transform(sample);
                sample = _filters[channel, 2].Transform(sample);
            }

            sample *= trim;
            buffer[offset + n] = Math.Clamp(sample, -1.0f, 1.0f);
        }

        return samplesRead;
    }

    private float GetTrimFactor()
    {
        double maxBoost = Math.Max(0, Math.Max(_bassDb, Math.Max(_midDb, _trebleDb)));
        return maxBoost >= 8 ? 0.75f : maxBoost >= 5 ? 0.85f : 1.0f;
    }
}

public sealed record DemucsCommand(string FileName, string ArgumentsPrefix);

public sealed record DemucsProgress(int Percent, string Message);

public abstract class NotifyModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
