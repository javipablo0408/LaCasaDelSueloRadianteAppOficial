using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

public class ImageSyncService : IDisposable
{
    private readonly string _localFolderPath = @"C:\Users\Javier\AppData\Local\Packages\com.companyname.lacasadelsueloradianteapp_9zz4h110yvjzm\LocalState";
    private readonly string _oneDriveFolderPath = "/lacasadelsueloradianteapp/";
    private readonly HttpClient _httpClient;
    private readonly Func<Task<string>> _getAccessToken; // Función para obtener el token de acceso
    private Timer _syncTimer;

    public ImageSyncService(HttpClient httpClient, Func<Task<string>> getAccessToken)
    {
        _httpClient = httpClient;
        _getAccessToken = getAccessToken;
    }

    public void Start()
    {
        _syncTimer = new Timer(async _ => await SyncAsync(), null, TimeSpan.Zero, TimeSpan.FromMinutes(1));
        Debug.WriteLine("ImageSyncService iniciado. Sincronización cada 1 minuto.");
    }

    public void Stop()
    {
        _syncTimer?.Change(Timeout.Infinite, 0);
        Debug.WriteLine("ImageSyncService detenido.");
    }

    private async Task SyncAsync()
    {
        try
        {
            Debug.WriteLine("Iniciando sincronización...");

            var accessToken = await _getAccessToken();
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            // Sincronizar subida
            await UploadLocalImagesAsync();

            // Sincronizar descarga
            await DownloadRemoteImagesAsync();

            Debug.WriteLine("Sincronización completada.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error durante la sincronización: {ex.Message}");
        }
    }

    private async Task UploadLocalImagesAsync()
    {
        var localFiles = Directory.GetFiles(_localFolderPath, "*.jpg");

        foreach (var localFile in localFiles)
        {
            var fileName = Path.GetFileName(localFile);
            var remoteFileMetadata = await GetRemoteFileMetadataAsync(fileName);

            if (remoteFileMetadata == null || IsLocalFileNewer(localFile, remoteFileMetadata))
            {
                await UploadFileToOneDriveAsync(localFile, fileName);
            }
        }
    }

    private async Task DownloadRemoteImagesAsync()
    {
        var remoteFiles = await GetRemoteFilesAsync();

        foreach (var remoteFile in remoteFiles)
        {
            var localFilePath = Path.Combine(_localFolderPath, remoteFile.Name);

            if (!File.Exists(localFilePath) || IsRemoteFileNewer(localFilePath, remoteFile))
            {
                await DownloadFileFromOneDriveAsync(remoteFile.Id, localFilePath);
            }
        }
    }

    private async Task UploadFileToOneDriveAsync(string localFilePath, string fileName)
    {
        try
        {
            var fileContent = await File.ReadAllBytesAsync(localFilePath);
            var requestUri = $"https://graph.microsoft.com/v1.0/me/drive/root:{_oneDriveFolderPath}{fileName}:/content";

            using var content = new ByteArrayContent(fileContent);
            var response = await _httpClient.PutAsync(requestUri, content);

            if (response.IsSuccessStatusCode)
            {
                Debug.WriteLine($"Archivo subido: {fileName}");
            }
            else
            {
                Debug.WriteLine($"Error al subir {fileName}: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error al subir {fileName}: {ex.Message}");
        }
    }

    private async Task DownloadFileFromOneDriveAsync(string fileId, string localFilePath)
    {
        try
        {
            var requestUri = $"https://graph.microsoft.com/v1.0/me/drive/items/{fileId}/content";
            var response = await _httpClient.GetAsync(requestUri);

            if (response.IsSuccessStatusCode)
            {
                var fileContent = await response.Content.ReadAsByteArrayAsync();
                await File.WriteAllBytesAsync(localFilePath, fileContent);
                Debug.WriteLine($"Archivo descargado: {localFilePath}");
            }
            else
            {
                Debug.WriteLine($"Error al descargar {localFilePath}: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error al descargar {localFilePath}: {ex.Message}");
        }
    }

    private async Task<dynamic> GetRemoteFileMetadataAsync(string fileName)
    {
        var requestUri = $"https://graph.microsoft.com/v1.0/me/drive/root:{_oneDriveFolderPath}{fileName}";
        var response = await _httpClient.GetAsync(requestUri);

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<dynamic>(json);
        }

        return null;
    }

    private async Task<dynamic[]> GetRemoteFilesAsync()
    {
        var requestUri = $"https://graph.microsoft.com/v1.0/me/drive/root:{_oneDriveFolderPath}:/children";
        var response = await _httpClient.GetAsync(requestUri);

        if (response.IsSuccessStatusCode)
        {
            var json = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<dynamic>(json);
            return result?.value?.ToArray() ?? Array.Empty<dynamic>();
        }

        return Array.Empty<dynamic>();
    }

    private bool IsLocalFileNewer(string localFilePath, dynamic remoteFileMetadata)
    {
        var localLastModified = File.GetLastWriteTimeUtc(localFilePath);
        var remoteLastModified = DateTime.Parse(remoteFileMetadata.lastModifiedDateTime.ToString());
        return localLastModified > remoteLastModified;
    }

    private bool IsRemoteFileNewer(string localFilePath, dynamic remoteFileMetadata)
    {
        var localLastModified = File.GetLastWriteTimeUtc(localFilePath);
        var remoteLastModified = DateTime.Parse(remoteFileMetadata.lastModifiedDateTime.ToString());
        return remoteLastModified > localLastModified;
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
    }
}