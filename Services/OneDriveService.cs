using Microsoft.Identity.Client;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class OneDriveService
    {
        private readonly MauiMsalAuthService _authService;
        private readonly HttpClient _httpClient;
        private const string GraphApiBaseUrl = "https://graph.microsoft.com/v1.0/";

        public OneDriveService(MauiMsalAuthService authService)
        {
            _authService = authService;
            _httpClient = new HttpClient { BaseAddress = new Uri(GraphApiBaseUrl) };
        }

        private async Task SetAuthHeaderAsync()
        {
            var token = await _authService.AcquireTokenAsync();
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token.AccessToken);
        }

        // Sube un archivo a OneDrive dentro de la carpeta "Lacasadelsueloradianteapp".
        public async Task UploadFileAsync(string fileName, Stream content)
        {
            await SetAuthHeaderAsync();
            var folderId = await EnsureAppFolderAsync();

            var requestUrl = $"me/drive/items/{folderId}:/{fileName}:/content";
            var streamContent = new StreamContent(content);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

            var response = await _httpClient.PutAsync(requestUrl, streamContent);
            response.EnsureSuccessStatusCode();
        }

        // Retorna una lista de archivos (DriveItem) en la carpeta "Lacasadelsueloradianteapp".
        public async Task<List<DriveItem>> ListFilesAsync()
        {
            await SetAuthHeaderAsync();
            var folderId = await EnsureAppFolderAsync();
            var response = await _httpClient.GetAsync($"me/drive/items/{folderId}/children");
            response.EnsureSuccessStatusCode();
            var content = await response.Content.ReadAsStringAsync();
            var driveResponse = JsonSerializer.Deserialize<DriveItemResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return driveResponse?.Value;
        }

        // Descarga un archivo de OneDrive.
        public async Task<byte[]> DownloadFileAsync(string fileId)
        {
            await SetAuthHeaderAsync();
            var response = await _httpClient.GetAsync($"me/drive/items/{fileId}/content");
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsByteArrayAsync();
        }

        // Método de ejemplo para sincronizar fotos.
        public async Task SincronizarFotosAsync(params string[] photoUrls)
        {
            foreach (var url in photoUrls)
            {
                if (!string.IsNullOrEmpty(url))
                {
                    // Aquí se puede implementar la lógica para sincronizar cada foto.
                    // Por ejemplo: descargar el contenido desde la URL y luego re-subirlo.
                }
            }
        }

        // Obtiene o crea la carpeta "Lacasadelsueloradianteapp" en OneDrive.
        private async Task<string> EnsureAppFolderAsync()
        {
            var folderName = "Lacasadelsueloradianteapp";
            await SetAuthHeaderAsync();

            // Buscar la carpeta con el nombre exacto.
            var response = await _httpClient.GetAsync($"me/drive/root/children?$filter=name eq '{folderName}'");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var driveResponse = JsonSerializer.Deserialize<DriveItemResponse>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (driveResponse?.Value != null && driveResponse.Value.Count > 0)
                {
                    return driveResponse.Value[0].Id;
                }
            }

            // Si la carpeta no existe, se crea.
            var folderJson = $"{{\"name\": \"{folderName}\", \"folder\": {{}}, \"@microsoft.graph.conflictBehavior\": \"rename\"}}";
            var createResponse = await _httpClient.PostAsync("me/drive/root/children", new StringContent(folderJson, Encoding.UTF8, "application/json"));
            createResponse.EnsureSuccessStatusCode();
            var createdContent = await createResponse.Content.ReadAsStringAsync();
            var createdDriveItem = JsonSerializer.Deserialize<DriveItem>(createdContent, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            return createdDriveItem.Id;
        }
    }

    // Clases auxiliares para deserializar respuestas de Microsoft Graph.
    public class DriveItemResponse
    {
        public List<DriveItem> Value { get; set; }
    }

    public class DriveItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
    }
}