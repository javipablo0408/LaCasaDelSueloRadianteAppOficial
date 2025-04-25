using SQLite;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LaCasaDelSueloRadianteApp.Services; // Para MauiMsalAuthService
using LaCasaDelSueloRadianteApp;           // Para Cliente y Servicio

namespace LaCasaDelSueloRadianteApp.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _connection;
        private readonly MauiMsalAuthService _authService;

        // Ahora recibe también el MauiMsalAuthService inyectado
        public DatabaseService(string dbPath, MauiMsalAuthService authService)
        {
            _connection = new SQLiteAsyncConnection(dbPath);
            _connection.CreateTableAsync<Cliente>().Wait();
            _connection.CreateTableAsync<Servicio>().Wait();

            _authService = authService
                ?? throw new ArgumentNullException(nameof(authService));
        }

        // Guarda un nuevo cliente
        public Task<int> GuardarClienteAsync(Cliente cliente) =>
            _connection.InsertAsync(cliente);

        // Guarda un nuevo servicio
        public Task<int> GuardarServicioAsync(Servicio servicio) =>
            _connection.InsertAsync(servicio);

        // Lista todos los clientes
        public Task<List<Cliente>> ObtenerClientesAsync() =>
            _connection.Table<Cliente>().ToListAsync();

        // Lista servicios de un cliente
        public Task<List<Servicio>> ObtenerServiciosAsync(int clienteId) =>
            _connection.Table<Servicio>()
                       .Where(s => s.ClienteId == clienteId)
                       .ToListAsync();

        // Serializa clientes+servicios y sube JSON a OneDrive
        public async Task SincronizarConOneDriveAsync()
        {
            var clientes = await ObtenerClientesAsync();
            var datosSincronizados = new List<object>();

            foreach (var cliente in clientes)
            {
                var servicios = await ObtenerServiciosAsync(cliente.Id);
                datosSincronizados.Add(new { Cliente = cliente, Servicios = servicios });
            }

            string json = JsonSerializer.Serialize(datosSincronizados);
            await UploadToOneDrive("sincronizacion.json", json);
        }

        // Sube el archivo JSON a OneDrive usando el mismo singleton de authService
        private async Task UploadToOneDrive(string fileName, string content)
        {
            // Reutiliza la instancia inyectada de MauiMsalAuthService
            var authResult = await _authService.AcquireTokenAsync();
            var accessToken = authResult.AccessToken;

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var requestUri =
                $"https://graph.microsoft.com/v1.0/me/drive/root:/Lacasadelsueloradianteapp/{fileName}:/content";

            using var stringContent =
                new StringContent(content, Encoding.UTF8, "application/json");

            var response = await client.PutAsync(requestUri, stringContent);
            if (!response.IsSuccessStatusCode)
                throw new Exception(
                    $"Error al subir el archivo a OneDrive: {response.ReasonPhrase}");
        }
    }
}