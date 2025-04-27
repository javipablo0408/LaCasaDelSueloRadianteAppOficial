using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Graph.Models; // Se asume que se usa Microsoft.Graph.Models para otros modelos

namespace LaCasaDelSueloRadianteApp.Services
{
    public class OneDriveService
    {
        private readonly MauiMsalAuthService _auth;
        private readonly HttpClient _http;

        public OneDriveService(MauiMsalAuthService auth)
        {
            _auth = auth ?? throw new ArgumentNullException(nameof(auth));
            _http = new HttpClient();
        }

        /// <summary>
        /// Agrega la cabecera de autorización (Bearer) al cliente HTTP usando el token obtenido.
        /// </summary>
        private async Task AddAuthHeaderAsync()
        {
            var authResult = await _auth.AcquireTokenAsync();
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        }

        public async Task UploadFileAsync(string remotePath, Stream content)
        {
            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota no puede ser nula o vacía.", nameof(remotePath));

            await AddAuthHeaderAsync();

            // Se asume que la implementación de carga pequeña consiste en un PUT al endpoint de OneDrive.
            var requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/content";
            using var streamContent = new StreamContent(content);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");

            var response = await _http.PutAsync(requestUrl, streamContent);
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Error al subir el archivo: {response.ReasonPhrase}");
            }
        }

        public async Task UploadLargeFileAsync(string remotePath, Stream content)
        {
            // Aquí se debe implementar la lógica de carga fragmentada para archivos grandes.
            // Se asume que ya existe esa implementación o se delega a otra clase.
            throw new NotImplementedException("La carga de archivos grandes aún no se ha implementado.");
        }

        /// <summary>
        /// Crea un enlace compartido para el archivo ubicado en la ruta remota especificada.
        /// Se utiliza el endpoint de createLink de Microsoft Graph para recursos basados en rutas.
        /// </summary>
        /// <param name="remotePath">Ruta remota del archivo en OneDrive.</param>
        /// <param name="ct">Token de cancelación.</param>
        /// <returns>URL del enlace compartido.</returns>
        public async Task<string> CreateShareLinkAsync(string remotePath, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(remotePath))
                throw new ArgumentException("La ruta remota no puede ser nula o vacía.", nameof(remotePath));

            await AddAuthHeaderAsync();

            // Se utiliza el endpoint basado en la ruta para crear un enlace compartido.
            var requestUrl = $"https://graph.microsoft.com/v1.0/me/drive/root:/{remotePath}:/createLink";

            // Se especifica el tipo de enlace, por ejemplo "view" para visualización.
            var body = new { type = "view" };
            string jsonBody = JsonSerializer.Serialize(body);
            using var contentJson = new StringContent(jsonBody, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await _http.PostAsync(requestUrl, contentJson, ct);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"Error al crear el enlace compartido: {response.ReasonPhrase}");

            string responseContent = await response.Content.ReadAsStringAsync(ct);

            // Se define una clase interna para deserializar la respuesta.
            var permission = JsonSerializer.Deserialize<PermissionResponse>(responseContent, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (permission?.Link == null || string.IsNullOrEmpty(permission.Link.WebUrl))
                throw new Exception("No se obtuvo el enlace compartido.");

            return permission.Link.WebUrl;
        }

        // Clases auxiliares para la deserialización de la respuesta del createLink
        private class PermissionResponse
        {
            public ShareLink? Link { get; set; }
        }

        private class ShareLink
        {
            public string? WebUrl { get; set; }
        }
    }
}
