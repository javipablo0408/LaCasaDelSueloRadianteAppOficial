using SQLite;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly OneDriveService _oneDrive;
        private SQLiteAsyncConnection _conn;

        public DatabaseService(string dbPath, OneDriveService oneDrive)
        {
            _dbPath = dbPath ?? throw new ArgumentNullException(nameof(dbPath));
            _oneDrive = oneDrive ?? throw new ArgumentNullException(nameof(oneDrive));
        }

        /* ----------- inicialización diferida ----------- */
        public async Task InitAsync()
        {
            // Verificar si la base de datos local existe
            if (!File.Exists(_dbPath))
            {
                Console.WriteLine("Base de datos no encontrada. Intentando restaurar desde OneDrive...");

                try
                {
                    // Restaurar la base de datos desde OneDrive
                    await _oneDrive.RestaurarBaseDeDatosAsync(_dbPath);
                    Console.WriteLine("Base de datos restaurada exitosamente.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error al restaurar la base de datos: {ex.Message}");
                    throw;
                }
            }

            // Inicializar la conexión SQLite
            _conn = new SQLiteAsyncConnection(_dbPath);
            await _conn.CreateTableAsync<Cliente>();
            await _conn.CreateTableAsync<Servicio>();
        }

        /* ----------- backup a OneDrive (copia temporal) ----------- */
        private async Task BackupAsync()
        {
            try
            {
                var tmp = Path.Combine(Path.GetTempPath(), "clientes.db3");
                File.Copy(_dbPath, tmp, true);

                await using var fs = File.OpenRead(tmp);
                var remote = "lacasadelsueloradianteapp/clientes.db3";

                if (fs.Length <= 4 * 1024 * 1024)
                    await _oneDrive.UploadFileAsync(remote, fs);
                else
                    await _oneDrive.UploadLargeFileAsync(remote, fs);

                File.Delete(tmp);
            }
            catch
            {
                /* Ignorar errores de backup */
            }
        }

        /* ----------- manejo de imágenes ----------- */
        public async Task<string> GuardarImagenAsync(string localPath, string remoteFolder)
        {
            try
            {
                // Subir la imagen a OneDrive
                var remotePath = $"{remoteFolder}/{Path.GetFileName(localPath)}";
                await using var stream = File.OpenRead(localPath);
                await _oneDrive.UploadFileAsync(remotePath, stream);

                return remotePath; // Retornar la ruta remota en OneDrive
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar la imagen: {ex.Message}");
                throw;
            }
        }

        public async Task DescargarImagenSiNoExisteAsync(string localPath, string remotePath)
        {
            try
            {
                if (!File.Exists(localPath))
                {
                    await _oneDrive.DescargarImagenSiNoExisteAsync(localPath, remotePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al descargar la imagen: {ex.Message}");
                throw;
            }
        }

        public Task<List<Servicio>> ObtenerTodosLosServiciosAsync()
        {
            return _conn.Table<Servicio>().ToListAsync();
        }

        /* ----------- API pública ----------- */

        // Guardar un nuevo cliente
        public async Task<int> GuardarClienteAsync(Cliente c)
        {
            var id = await _conn.InsertAsync(c);
            await BackupAsync();
            return id;
        }

        // Guardar un nuevo servicio
        public async Task<int> GuardarServicioAsync(Servicio s)
        {
            var id = await _conn.InsertAsync(s);
            await BackupAsync();
            return id;
        }

        // Obtener todos los clientes
        public Task<List<Cliente>> ObtenerClientesAsync() =>
            _conn.Table<Cliente>().ToListAsync();

        // Obtener servicios de un cliente específico
        public Task<List<Servicio>> ObtenerServiciosAsync(int clienteId) =>
            _conn.Table<Servicio>()
                 .Where(s => s.ClienteId == clienteId)
                 .ToListAsync();

        // Actualizar un cliente existente
        public async Task<int> ActualizarClienteAsync(Cliente c)
        {
            var rowsAffected = await _conn.UpdateAsync(c);
            await BackupAsync();
            return rowsAffected;
        }

        // Actualizar un servicio existente
        public async Task<int> ActualizarServicioAsync(Servicio s)
        {
            var rowsAffected = await _conn.UpdateAsync(s);
            await BackupAsync();
            return rowsAffected;
        }

        // Eliminar un cliente
        public async Task<int> EliminarClienteAsync(Cliente c)
        {
            var rowsAffected = await _conn.DeleteAsync(c);
            await BackupAsync();
            return rowsAffected;
        }

        // Eliminar un servicio
        public async Task<int> EliminarServicioAsync(Servicio s)
        {
            var rowsAffected = await _conn.DeleteAsync(s);
            await BackupAsync();
            return rowsAffected;
        }
    }
}