using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace AkariDash.Core.Machine;

/// <summary>
/// AkariOS-Ultimate's black images, done with Windows' own image codecs (WIC, through WinRT) where it
/// used System.Drawing: the sign-out wallpaper file, and the default account pictures painted black
/// over a backup.
/// </summary>
internal static class BlackImages
{
    private static string Pictures => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\User Account Pictures");

    // AkariOS-Ultimate's backup folder: ProgramData\User Account Pictures.
    private static string Backup => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "User Account Pictures");

    public static BlackImage? ReadFile(BlackImageLocation location) => File.Exists(location.Path) ? BlackImage.Black : null;

    /// <summary>A black JPEG the size AkariOS-Ultimate makes it: the primary screen's, 1920 x 1080 when that cannot be read.</summary>
    public static void WriteFile(BlackImageLocation location)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(location.Path)!);
        var (width, height) = PrimaryScreen();
        Encode(location.Path, BitmapEncoder.JpegEncoderId, width, height);
    }

    public static void DeleteFile(BlackImageLocation location) => File.Delete(location.Path);

    public static BlackImage? ReadAccountPictures() => Directory.Exists(Backup) ? BlackImage.Black : null;

    public static void PaintAccountPictures()
    {
        // Back up once, as AkariOS-Ultimate does, so painting twice never backs up the black ones.
        if (!Directory.Exists(Backup))
        {
            CopyTree(Pictures, Backup);
        }

        foreach (var file in Directory.EnumerateFiles(Pictures, "*.*", SearchOption.AllDirectories))
        {
            var encoder = Path.GetExtension(file).ToLowerInvariant() switch
            {
                ".png" => BitmapEncoder.PngEncoderId,
                ".bmp" => BitmapEncoder.BmpEncoderId,
                _ => (Guid?)null,
            };

            if (encoder is { } id && SizeOf(file) is var (width, height))
            {
                Encode(file, id, width, height);
            }
        }
    }

    public static void RestoreAccountPictures()
    {
        if (!Directory.Exists(Backup))
        {
            return;
        }

        CopyTree(Backup, Pictures);
        Directory.Delete(Backup, recursive: true);
    }

    private static void CopyTree(string from, string to)
    {
        foreach (var file in Directory.EnumerateFiles(from, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(to, Path.GetRelativePath(from, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static (uint Width, uint Height)? SizeOf(string file)
    {
        try
        {
            return Run(async () =>
            {
                using var stream = File.OpenRead(file).AsRandomAccessStream();
                var decoder = await BitmapDecoder.CreateAsync(stream);
                return ((uint, uint)?)(decoder.PixelWidth, decoder.PixelHeight);
            });
        }
        catch
        {
            // AkariOS-Ultimate skips a picture it cannot open, and so does this.
            return null;
        }
    }

    private static void Encode(string file, Guid encoderId, uint width, uint height) => Run(async () =>
    {
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
        // BGRA, all zero: black, with alpha ignored so it is opaque.
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, width, height, 96, 96, new byte[width * height * 4]);
        await encoder.FlushAsync();

        stream.Seek(0);
        await using var target = File.Create(file);
        await stream.AsStreamForRead().CopyToAsync(target);
        return true;
    });

    private static (uint, uint) PrimaryScreen()
    {
        var width = GetSystemMetrics(0);
        var height = GetSystemMetrics(1);
        return width > 0 && height > 0 ? ((uint)width, (uint)height) : (1920, 1080);
    }

    // The WinRT codecs are async; the machine is not. Off the caller's thread, so a UI thread waiting
    // here cannot deadlock the continuation.
    private static T Run<T>(Func<Task<T>> work) => Task.Run(work).GetAwaiter().GetResult();

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);
}
