using System.Net.Http;
using NativeMedia;
using System.IO;

namespace LaCasaDelSueloRadianteApp.Services;

public interface IImageService
{
    Task<string?> DownloadAndSaveAsync(string url, IProgress<double>? progress = null);
}

public sealed class ImageService : IImageService
{
    readonly HttpClient _http = new();

    public async Task<string?> DownloadAndSaveAsync(string url, IProgress<double>? progress = null)
    {
        using var resp = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();

        long total = resp.Content.Headers.ContentLength ?? -1;
        var ms = new MemoryStream();
        var buf = new byte[8192];
        long read = 0;

        await using var stream = await resp.Content.ReadAsStreamAsync();
        int n;
        while ((n = await stream.ReadAsync(buf)) > 0)
        {
            await ms.WriteAsync(buf.AsMemory(0, n));
            read += n;
            if (total > 0) progress?.Report((double)read / total);
        }

        var bytes = ms.ToArray();
        var fileName = Path.GetFileName(new Uri(url).LocalPath);

#if ANDROID || IOS
        // Fix: Use the correct overload of MediaGallery.SaveAsync that accepts a MediaFileType and a Stream.
        using var byteStream = new MemoryStream(bytes);
        await MediaGallery.SaveAsync(MediaFileType.Image, byteStream, fileName);
        return fileName; // Return the file name or adjust as needed.
#else
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        Directory.CreateDirectory(downloads);
        var path = Path.Combine(downloads, fileName);
        await File.WriteAllBytesAsync(path, bytes);
        return path;
#endif
    }
}
