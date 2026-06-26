using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace SomDeFundoCSharp;

public sealed record CoverPreview(BitmapSource Source, int PixelWidth, int PixelHeight);

public sealed record CoverCropRequest(string SourcePath, double X, double Y, double Size);

public static class CoverImageService
{
    public const int CoverSize = 256;
    public const long MaxCoverBytes = 10 * 1024 * 1024;
    private const string DefaultCoverPackUri = "pack://application:,,,/Assets/default-music-cover.png";
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp"
    };

    public static ImageSource LoadCoverImage(string? customPath)
    {
        if (IsUsableCustomCover(customPath))
        {
            try
            {
                return LoadBitmapImage(new Uri(Path.GetFullPath(customPath!)));
            }
            catch
            {
                return LoadDefaultCoverImage();
            }
        }

        return LoadDefaultCoverImage();
    }

    public static ImageSource LoadDefaultCoverImage() => LoadBitmapImage(new Uri(DefaultCoverPackUri, UriKind.Absolute));

    public static string GetCoverLabel(string? customPath)
    {
        return IsUsableCustomCover(customPath) ? Path.GetFileName(customPath!) : "Capa padrao";
    }

    public static bool IsUsableCustomCover(string? customPath)
    {
        if (string.IsNullOrWhiteSpace(customPath) || customPath.StartsWith("offline://", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            if (!File.Exists(customPath))
            {
                return false;
            }

            string extension = Path.GetExtension(customPath);
            if (!AllowedExtensions.Contains(extension))
            {
                return false;
            }

            _ = Image.Identify(customPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string? NormalizeStoredCoverPath(string? customPath)
    {
        return IsUsableCustomCover(customPath) ? Path.GetFullPath(customPath!) : null;
    }

    public static void ValidateSourceFile(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            throw new InvalidOperationException("Arquivo de imagem nao encontrado.");
        }

        string extension = Path.GetExtension(sourcePath);
        if (!AllowedExtensions.Contains(extension))
        {
            throw new InvalidOperationException("Formato invalido. Use JPG, JPEG, PNG ou WebP.");
        }

        var fileInfo = new FileInfo(sourcePath);
        if (fileInfo.Length > MaxCoverBytes)
        {
            throw new InvalidOperationException("A imagem deve ter no maximo 10 MB.");
        }

        try
        {
            using var image = Image.Load(sourcePath);
            image.Mutate(x => x.AutoOrient());
        }
        catch
        {
            throw new InvalidOperationException("Arquivo de imagem invalido ou corrompido.");
        }
    }

    public static CoverPreview LoadCropPreview(string sourcePath)
    {
        ValidateSourceFile(sourcePath);

        using var image = Image.Load<SixLabors.ImageSharp.PixelFormats.Rgba32>(sourcePath);
        image.Mutate(x => x.AutoOrient());

        using var stream = new MemoryStream();
        image.SaveAsPng(stream);
        stream.Position = 0;

        var bitmap = new BitmapImage();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        return new CoverPreview(bitmap, image.Width, image.Height);
    }

    public static string CropResizeAndSave(CoverCropRequest request, string targetDirectory)
    {
        ValidateSourceFile(request.SourcePath);
        Directory.CreateDirectory(targetDirectory);

        using var image = Image.Load(request.SourcePath);
        image.Mutate(x => x.AutoOrient());

        int x = (int)Math.Clamp(Math.Round(request.X), 0, Math.Max(0, image.Width - 1));
        int y = (int)Math.Clamp(Math.Round(request.Y), 0, Math.Max(0, image.Height - 1));
        int size = (int)Math.Clamp(Math.Round(request.Size), 1, Math.Min(image.Width - x, image.Height - y));

        image.Mutate(operation => operation
            .Crop(new Rectangle(x, y, size, size))
            .Resize(CoverSize, CoverSize));

        string baseName = CreateSafeBaseName(Path.GetFileNameWithoutExtension(request.SourcePath));
        string webpPath = CreateUniquePath(targetDirectory, baseName, ".webp");

        try
        {
            image.Save(webpPath, new WebpEncoder { Quality = 82 });
            return webpPath;
        }
        catch
        {
            string jpegPath = CreateUniquePath(targetDirectory, baseName, ".jpg");
            image.Save(jpegPath, new JpegEncoder { Quality = 85 });
            return jpegPath;
        }
    }

    private static BitmapImage LoadBitmapImage(Uri uri)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        image.UriSource = uri;
        image.DecodePixelWidth = CoverSize;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static string CreateSafeBaseName(string? name)
    {
        string safeName = string.Join("_", (name ?? "cover").Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries));
        if (string.IsNullOrWhiteSpace(safeName))
        {
            safeName = "cover";
        }

        return safeName.Length > 40 ? safeName[..40] : safeName;
    }

    private static string CreateUniquePath(string targetDirectory, string baseName, string extension)
    {
        for (int attempt = 0; attempt < 20; attempt++)
        {
            string suffix = $"{DateTime.Now:yyyyMMddHHmmssfff}_{Random.Shared.Next(1000, 9999)}";
            string path = Path.Combine(targetDirectory, $"{baseName}_{suffix}{extension}");
            if (!File.Exists(path))
            {
                return path;
            }
        }

        return Path.Combine(targetDirectory, $"{baseName}_{Guid.NewGuid():N}{extension}");
    }
}
