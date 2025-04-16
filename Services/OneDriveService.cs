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
        private const string AppFolderName = "Lacasadelsueloradianteapp";

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

        /// <summary>
        /// Busca o crea la carpeta de la app y devuelve su Id.
        /// </summary>
        public async Task<string> EnsureAppFolderAsync()
        {
            await SetAuthHeaderAsync();

            // Buscar si la carpeta ya existe
            var response = await _httpClient.GetAsync("me/drive/root/children?$filter=folder ne null");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GraphResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            var folder = result?.Value?.FirstOrDefault(f => f.Name == AppFolderName && f.Folder != null);
            if (folder != null)
                return folder.Id;

            // Si no existe, crearla
            var json = $@"
{{
    ""name"": ""{AppFolderName}"",
    ""folder"": {{}},
    ""@microsoft.graph.conflictBehavior"": ""rename""
}}";
            var createResponse = await _httpClient.PostAsync("me/drive/root/children", new StringContent(json, System.Text.Encoding.UTF8, "application/json"));
            createResponse.EnsureSuccessStatusCode();
            var createContent = await createResponse.Content.ReadAsStringAsync();
            var createdFolder = JsonSerializer.Deserialize<DriveItem>(createContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return createdFolder.Id;
        }

        /// <summary>
        /// Lista los archivos SOLO de la carpeta de la app.
        /// </summary>
        public async Task<IEnumerable<DriveItem>> ListFilesAsync()
        {
            await SetAuthHeaderAsync();
            var folderId = await EnsureAppFolderAsync();

            var response = await _httpClient.GetAsync($"me/drive/items/{folderId}/children");
            response.EnsureSuccessStatusCode();

            var content = await response.Content.ReadAsStringAsync();
            var result = JsonSerializer.Deserialize<GraphResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result?.Value ?? new List<DriveItem>();
        }

        /// <summary>
        /// Sube un archivo a la carpeta de la app y devuelve su Id.
        /// </summary>
        public async Task<string> UploadFileAsync(string fileName, Stream content)
        {
            await SetAuthHeaderAsync();
            var folderId = await EnsureAppFolderAsync();

            var requestUrl = $"me/drive/items/{folderId}:/{fileName}:/content";
            var streamContent = new StreamContent(content);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PutAsync(requestUrl, streamContent);
            response.EnsureSuccessStatusCode();

            var responseContent = await response.Content.ReadAsStringAsync();
            var uploadedItem = JsonSerializer.Deserialize<DriveItem>(responseContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return uploadedItem.Id;
        }

        /// <summary>
        /// Obtiene un enlace compartido para un archivo.
        /// </summary>
        public async Task<string> GetShareLinkAsync(string itemId)
        {
            await SetAuthHeaderAsync();

            var json = @"{ ""type"": ""view"", ""scope"": ""anonymous"" }";
            var response = await _httpClient.PostAsync(
                $"me/drive/items/{itemId}/createLink",
                new StringContent(json, System.Text.Encoding.UTF8, "application/json"));

            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(content);
            var url = doc.RootElement.GetProperty("link").GetProperty("webUrl").GetString();
            return url ?? "";
        }

        /// <summary>
        /// Descarga un archivo de la carpeta de la app.
        /// </summary>
        public async Task<Stream> DownloadFileAsync(string itemId)
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"me/drive/items/{itemId}/content");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// Cierra la sesión del usuario.
        /// </summary>
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
            public FileFacet File { get; set; }
            public FolderFacet Folder { get; set; }
            public bool IsFolder => Folder != null;
        }

        public class FileFacet
        {
            public string MimeType { get; set; }
        }

        public class FolderFacet
        {
            public int? ChildCount { get; set; }
        }
    }
}