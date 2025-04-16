using Microsoft.Graph.Models;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using System.Text.Json;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class OneDriveService
    {
        private readonly HttpClient _httpClient;
        private readonly MauiMsalAuthService _authService;
        private const string GraphApiBaseUrl = "https://graph.microsoft.com/v1.0/";

        public OneDriveService(MauiMsalAuthService authService)
        {
            _authService = authService;
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(GraphApiBaseUrl)
            };
        }

        private async Task SetAuthHeaderAsync()
        {
            var token = await _authService.AcquireTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.AccessToken);
        }

        public async Task<IEnumerable<DriveItem>> ListFilesAsync()
        {
            try
            {
                await SetAuthHeaderAsync();
                var response = await _httpClient.GetAsync("me/drive/root/children");
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<GraphResponse>(content,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return result?.Value ?? new List<DriveItem>();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al listar archivos", ex);
            }
        }

        public async Task<Stream> DownloadFileAsync(string itemId)
        {
            try
            {
                await SetAuthHeaderAsync();
                var response = await _httpClient.GetAsync($"me/drive/items/{itemId}/content");
                response.EnsureSuccessStatusCode();
                return await response.Content.ReadAsStreamAsync();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al descargar el archivo", ex);
            }
        }

        public async Task UploadFileAsync(string fileName, Stream content)
        {
            try
            {
                await SetAuthHeaderAsync();
                var requestUrl = $"me/drive/root:/{fileName}:/content";

                var streamContent = new StreamContent(content);
                streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

                var response = await _httpClient.PutAsync(requestUrl, streamContent);
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                throw new Exception("Error al subir el archivo", ex);
            }
        }

        public async Task SignOutAsync()
        {
            await _authService.SignOutAsync();
        }

        // Clases auxiliares para deserialización
        private class GraphResponse
        {
            public List<DriveItem> Value { get; set; }
        }

        public class DriveItem
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public long? Size { get; set; }
            public DateTimeOffset? LastModifiedDateTime { get; set; }
            public FileSystemInfo File { get; set; }
            public FileSystemInfo Folder { get; set; }
            public bool IsFolder => Folder != null;
        }

        public class FileSystemInfo
        {
            public string MimeType { get; set; }
            public int? ChildCount { get; set; }
        }
    }
}