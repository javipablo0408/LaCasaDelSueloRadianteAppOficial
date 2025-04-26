using SQLite;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LaCasaDelSueloRadianteApp;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly SQLiteAsyncConnection _conn;
        private readonly OneDriveService _oneDrive;
        private const string DbFileName = "clientes.db3";
        private const string RemoteFolder = "lacasadelsueloradianteapp";

        public DatabaseService(string dbPath, OneDriveService oneDriveService)
        {
            _dbPath = dbPath
                        ?? throw new ArgumentNullException(nameof(dbPath));
            _oneDrive = oneDriveService
                        ?? throw new ArgumentNullException(nameof(oneDriveService));

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
                Debug.WriteLine($"[DB] No hay DB remota: {ex.Message}");
            }
        }

        private async Task BackupDatabaseAsync()
        {
            using var fs = File.OpenRead(_dbPath);
            var remotePath = $"{RemoteFolder}/{DbFileName}";
            await _oneDrive.UploadFileAsync(remotePath, fs);
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