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
        private readonly SQLiteAsyncConnection _conn;
        private readonly OneDriveService _oneDrive;

        private const string DbFileName = "clientes.db3";
        private const string RemoteFolder = "lacasadelsueloradianteapp";

        public DatabaseService(string dbPath, OneDriveService oneDrive)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
            _oneDrive = oneDrive ?? throw new ArgumentNullException(nameof(oneDrive));

            TryDownloadRemoteDatabase().Wait();

            _conn = new SQLiteAsyncConnection(_dbPath);
            _conn.CreateTableAsync<Cliente>().Wait();
            _conn.CreateTableAsync<Servicio>().Wait();
        }

        private async Task TryDownloadRemoteDatabase()
        {
            try
            {
                var remotePath = $"{RemoteFolder}/{DbFileName}";
                var data = await _oneDrive.DownloadAsync(remotePath);
                if (data?.Length > 0)
                    File.WriteAllBytes(_dbPath, data);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[DB] No se pudo descargar: {ex.Message}");
            }
        }

        private async Task BackupDatabaseAsync()
        {
            try
            {
                // Copiar la base de datos a un archivo temporal para evitar bloqueo
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
                Debug.WriteLine($"Error en backup: {ex.Message}");
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
            _conn.Table<Servicio>().Where(x => x.ClienteId == clienteId).ToListAsync();
    }
}