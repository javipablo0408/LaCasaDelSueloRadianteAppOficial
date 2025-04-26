using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly OneDriveService _oneDrive;
        private SQLiteAsyncConnection _conn;

        private const string DbFileName = "clientes.db3";
        private const string RemoteFolder = "lacasadelsueloradianteapp";

        public DatabaseService(string dbPath, OneDriveService oneDrive)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
            _oneDrive = oneDrive ?? throw new ArgumentNullException(nameof(oneDrive));
            _conn = new SQLiteAsyncConnection(_dbPath);
        }

        public async Task InitAsync()
        {
            try
            {
                await TryDownloadRemoteDatabase();
                await _conn.CreateTableAsync<Cliente>();
                await _conn.CreateTableAsync<Servicio>();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB] Error inicializando base de datos: {ex.Message}");
                throw; // Relanzar para que la UI pueda mostrar el error
            }
        }

        private async Task TryDownloadRemoteDatabase()
        {
            try
            {
                var remotePath = $"{RemoteFolder}/{DbFileName}";
                var data = await _oneDrive.DownloadAsync(remotePath);
                if (data?.Length > 0)
                {
                    File.WriteAllBytes(_dbPath, data);
                    // Recrear la conexión con el archivo descargado
                    _conn = new SQLiteAsyncConnection(_dbPath);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB] No se pudo descargar: {ex.Message}");
                // No relanzar - es normal que falle si es primera ejecución
            }
        }

        private async Task BackupDatabaseAsync()
        {
            try
            {
                // Copiar a temporal para evitar bloqueos
                var tempPath = Path.Combine(Path.GetTempPath(), DbFileName);
                File.Copy(_dbPath, tempPath, overwrite: true);

                await using var fs = File.OpenRead(tempPath);
                var remotePath = $"{RemoteFolder}/{DbFileName}";

                if (fs.Length <= 4 * 1024 * 1024)
                    await _oneDrive.UploadFileAsync(remotePath, fs);
                else
                    await _oneDrive.UploadLargeFileAsync(remotePath, fs);

                File.Delete(tempPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB] Error en backup: {ex.Message}");
                throw;
            }
        }

        public async Task<int> GuardarClienteAsync(Cliente c)
        {
            var r = await _conn.InsertAsync(c);
            await BackupDatabaseAsync();
            return r;
        }

        public async Task<int> GuardarServicioAsync(Servicio s)
        {
            var r = await _conn.InsertAsync(s);
            await BackupDatabaseAsync();
            return r;
        }

        public Task<List<Cliente>> ObtenerClientesAsync() =>
            _conn.Table<Cliente>().ToListAsync();

        public Task<List<Servicio>> ObtenerServiciosAsync(int clienteId) =>
            _conn.Table<Servicio>()
                 .Where(x => x.ClienteId == clienteId)
                 .ToListAsync();
    }
}