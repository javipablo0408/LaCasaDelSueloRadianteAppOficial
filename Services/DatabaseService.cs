using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;
using SQLite;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly OneDriveService _oneDrive;
        private readonly ILogger<DatabaseService> _logger;
        private SQLiteAsyncConnection _conn;
        private readonly string _tempDbName = "clientes_temp.db3";
        private readonly SemaphoreSlim _dbSemaphore = new(1, 1);

        public DatabaseService(OneDriveService oneDrive, ILogger<DatabaseService> logger)
        {
            AppPaths.EnsureDirectoriesExist();
            _dbPath = AppPaths.DatabasePath;
            _oneDrive = oneDrive ?? throw new ArgumentNullException(nameof(oneDrive));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task InitAsync(CancellationToken ct = default)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            if (File.Exists(_dbPath))
            {
                File.SetAttributes(_dbPath, FileAttributes.Normal);
                try
                {
                    var testConn = new SQLiteAsyncConnection(_dbPath);
                    await testConn.ExecuteScalarAsync<int>("PRAGMA user_version");
                }
                catch (SQLiteException)
                {
                    _logger.LogWarning("El archivo de base de datos es inválido o está corrupto. Se eliminará para crear uno nuevo.");
                    File.Delete(_dbPath);
                }
            }
            else
            {
                _logger.LogInformation("Base de datos local no encontrada. Buscando en OneDrive...");
                try
                {
                    await _oneDrive.RestaurarBaseDeDatosAsync(_dbPath);
                    File.SetAttributes(_dbPath, FileAttributes.Normal);
                    _logger.LogInformation("Base de datos restaurada desde OneDrive exitosamente.");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se encontró base de datos en OneDrive o error al restaurar. Se creará una base de datos nueva.");
                }
            }

            await EnsureConnectionAsync(ct);
        }

        private async Task EnsureConnectionAsync(CancellationToken ct = default)
        {
            if (_conn == null)
            {
                _conn = new SQLiteAsyncConnection(_dbPath);
                await _conn.CreateTableAsync<Cliente>();
                await _conn.CreateTableAsync<Servicio>();
            }
        }

        public async Task CerrarConexionAsync()
        {
            if (_conn != null)
            {
                await _conn.CloseAsync();
                _conn = null;
            }
        }

        // CRUD protegido solo para la operación crítica
        public async Task<int> GuardarClienteAsync(Cliente c, CancellationToken ct = default)
        {
            if (c == null)
                throw new ArgumentNullException(nameof(c), "El cliente no puede ser null.");

            int id;
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                id = await _conn.InsertAsync(c);
            }
            finally
            {
                _dbSemaphore.Release();
            }
            return id;
        }

        public async Task<int> GuardarServicioAsync(Servicio s, CancellationToken ct = default)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s), "El servicio no puede ser null.");

            int id;
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                id = await _conn.InsertAsync(s);
            }
            finally
            {
                _dbSemaphore.Release();
            }
            return id;
        }

        public async Task<List<Cliente>> ObtenerClientesAsync(CancellationToken ct = default)
        {
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                return await _conn.Table<Cliente>().ToListAsync();
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        public async Task<List<Servicio>> ObtenerServiciosAsync(int clienteId, CancellationToken ct = default)
        {
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                return await _conn.Table<Servicio>()
                                  .Where(s => s.ClienteId == clienteId)
                                  .ToListAsync();
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        public async Task<int> ActualizarClienteAsync(Cliente c, CancellationToken ct = default)
        {
            if (c == null)
                throw new ArgumentNullException(nameof(c), "El cliente no puede ser null.");

            int rowsAffected;
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                rowsAffected = await _conn.UpdateAsync(c);
            }
            finally
            {
                _dbSemaphore.Release();
            }
            return rowsAffected;
        }

        public async Task<int> ActualizarServicioAsync(Servicio s, CancellationToken ct = default)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s), "El servicio no puede ser null.");

            int rowsAffected;
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                rowsAffected = await _conn.UpdateAsync(s);
            }
            finally
            {
                _dbSemaphore.Release();
            }
            return rowsAffected;
        }

        public async Task<int> EliminarClienteAsync(Cliente c, CancellationToken ct = default)
        {
            if (c == null)
                throw new ArgumentNullException(nameof(c), "El cliente no puede ser null.");

            int rowsAffected;
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                rowsAffected = await _conn.DeleteAsync(c);
            }
            finally
            {
                _dbSemaphore.Release();
            }
            return rowsAffected;
        }

        public async Task<int> EliminarServicioAsync(Servicio s, CancellationToken ct = default)
        {
            if (s == null)
                throw new ArgumentNullException(nameof(s), "El servicio no puede ser null.");

            int rowsAffected;
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                rowsAffected = await _conn.DeleteAsync(s);
            }
            finally
            {
                _dbSemaphore.Release();
            }
            return rowsAffected;
        }

        // --- Métodos para sincronización a nivel de registros ---

        public async Task<List<Cliente>> ObtenerClientesNoSincronizadosAsync(CancellationToken ct = default)
        {
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                return await _conn.Table<Cliente>().Where(c => !c.IsSynced).ToListAsync();
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        public async Task<List<Servicio>> ObtenerServiciosNoSincronizadosAsync(CancellationToken ct = default)
        {
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                return await _conn.Table<Servicio>().Where(s => !s.IsSynced).ToListAsync();
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        public async Task MarcarClientesComoSincronizadosAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                foreach (var id in ids)
                {
                    var cliente = await _conn.FindAsync<Cliente>(id);
                    if (cliente != null)
                    {
                        cliente.IsSynced = true;
                        await _conn.UpdateAsync(cliente);
                    }
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        public async Task MarcarServiciosComoSincronizadosAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                foreach (var id in ids)
                {
                    var servicio = await _conn.FindAsync<Servicio>(id);
                    if (servicio != null)
                    {
                        servicio.IsSynced = true;
                        await _conn.UpdateAsync(servicio);
                    }
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        public async Task InsertarOActualizarClienteAsync(Cliente cliente, CancellationToken ct = default)
        {
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                var existente = await _conn.FindAsync<Cliente>(cliente.Id);
                if (existente == null)
                {
                    await _conn.InsertAsync(cliente);
                }
                else if (cliente.FechaModificacion > existente.FechaModificacion)
                {
                    await _conn.UpdateAsync(cliente);
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        public async Task InsertarOActualizarServicioAsync(Servicio servicio, CancellationToken ct = default)
        {
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                await EnsureConnectionAsync(ct);
                var existente = await _conn.FindAsync<Servicio>(servicio.Id);
                if (existente == null)
                {
                    await _conn.InsertAsync(servicio);
                }
                else if (servicio.FechaModificacion > existente.FechaModificacion)
                {
                    await _conn.UpdateAsync(servicio);
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        // --- Sincronización segura y restauración ---

        public async Task SincronizarBidireccionalAsync(CancellationToken ct = default)
        {
            await _dbSemaphore.WaitAsync(ct);
            try
            {
                if (_conn != null)
                {
                    await _conn.CloseAsync();
                    _conn = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    await Task.Delay(100);
                }

                // Llama a la sincronización bidireccional de OneDriveService
                await _oneDrive.SincronizarBidireccionalAsync(ct, this);

                // Reabre la conexión después de la sincronización
                _conn = new SQLiteAsyncConnection(_dbPath);
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        public void RestaurarBaseDeDatosDesdeContenido(string contenido)
        {
            _dbSemaphore.Wait();
            try
            {
                if (_conn != null)
                {
                    _conn.CloseAsync().GetAwaiter().GetResult();
                    _conn = null;
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    Thread.Sleep(100);
                }
                File.WriteAllText(_dbPath, contenido);
                File.SetAttributes(_dbPath, FileAttributes.Normal);
                _conn = new SQLiteAsyncConnection(_dbPath);
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }
    }
}