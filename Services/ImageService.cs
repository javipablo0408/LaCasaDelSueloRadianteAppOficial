using System.Net.Http;
using System.IO;
using Microsoft.Maui.Storage;

namespace LaCasaDelSueloRadianteApp.Services;

public interface IImageService
{
    /// <summary>
    /// Descarga una imagen desde una URL y la guarda en AppDataDirectory. Si ya existe, la retorna.
    /// </summary>
    /// <param name="url">URL de la imagen remota</param>
    /// <param name="progress">Progreso opcional</param>
    /// <returns>Nombre del archivo guardado (no ruta completa)</returns>
    Task<string?> DownloadAndSaveAsync(string url, IProgress<double>? progress = null);

    /// <summary>
    /// Devuelve la ruta local completa de una imagen guardada por nombre de archivo.
    /// </summary>
    /// <param name="fileName">Nombre del archivo</param>
    /// <returns>Ruta local completa</returns>
    string GetLocalPath(string fileName);
}

public sealed class ImageService : IImageService
{
    readonly HttpClient _http = new();

    public async Task<string?> DownloadAndSaveAsync(string url, IProgress<double>? progress = null)
    {
        var fileName = Path.GetFileName(new Uri(url).LocalPath);
        var localPath = Path.Combine(FileSystem.AppDataDirectory, fileName);

        if (File.Exists(localPath))
            return fileName;

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
        await File.WriteAllBytesAsync(localPath, bytes);

        return fileName;
    }

    public string GetLocalPath(string fileName)
    {
        return Path.Combine(FileSystem.AppDataDirectory, fileName);
    }
}