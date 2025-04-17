using SQLite;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using LaCasaDelSueloRadianteApp;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class DatabaseService
    {
        private readonly SQLiteAsyncConnection _connection;

        public DatabaseService(string dbPath)
        {
            _connection = new SQLiteAsyncConnection(dbPath);
            // Creación de las tablas para Cliente y Servicio.
            _connection.CreateTableAsync<Cliente>().Wait();
            _connection.CreateTableAsync<Servicio>().Wait();
        }

        // Método para guardar un nuevo cliente.
        public Task<int> GuardarClienteAsync(Cliente cliente) =>
            _connection.InsertAsync(cliente);

        // Método para guardar un nuevo servicio, relacionado con un cliente.
        public Task<int> GuardarServicioAsync(Servicio servicio) =>
            _connection.InsertAsync(servicio);

        // Obtiene la lista de clientes registrados.
        public Task<List<Cliente>> ObtenerClientesAsync() =>
            _connection.Table<Cliente>().ToListAsync();

        // Obtiene la lista de servicios asociados a un cliente en particular.
        public Task<List<Servicio>> ObtenerServiciosAsync(int clienteId) =>
            _connection.Table<Servicio>().Where(s => s.ClienteId == clienteId).ToListAsync();

        // Método de sincronización que agrupa clientes y sus servicios en formato JSON.
        // Además, se sube el JSON a OneDrive utilizando Microsoft Graph.
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
            // Subir el archivo JSON a OneDrive. El archivo se llamará "sincronizacion.json".
            await UploadToOneDrive("sincronizacion.json", json);
        }

        // Método privado que utiliza Microsoft Graph API para subir un archivo a OneDrive.
        // Se obtiene el token de acceso mediante la clase MauiMsalAuthService.
        private async Task UploadToOneDrive(string fileName, string content)
        {
            var authService = new MauiMsalAuthService();
            var authResult = await authService.AcquireTokenAsync();
            var accessToken = authResult.AccessToken;

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", accessToken);
                // Endpoint para subir el archivo a la raíz de OneDrive.
                var requestUri = $"https://graph.microsoft.com/v1.0/me/drive/root:/Lacasadelsueloradianteapp/{fileName}:/content";

                using (var stringContent = new StringContent(content, Encoding.UTF8, "application/json"))
                {
                    var response = await client.PutAsync(requestUri, stringContent);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"Error al subir el archivo a OneDrive: {response.ReasonPhrase}");
                    }
                }
            }
        }
    }
}