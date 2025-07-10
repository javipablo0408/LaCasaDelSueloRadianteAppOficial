using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;
using Microsoft.Extensions.Logging;
using SQLite;
using LaCasaDelSueloRadianteApp.Models;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly OneDriveService _oneDrive;
        private readonly ILogger<DatabaseService> _logger;
        private SQLiteAsyncConnection _conn;
        private readonly SemaphoreSlim _dbSemaphore = new(1, 1);
        private volatile bool _subidaBaseDatosPendiente = false;

        private const string RemoteFolder = "lacasadelsueloradianteapp";

        public DatabaseService(OneDriveService oneDrive, ILogger<DatabaseService> logger)
        {
            AppPaths.EnsureDirectoriesExist();
            _dbPath = AppPaths.DatabasePath;
            _oneDrive = oneDrive ?? throw new ArgumentNullException(nameof(oneDrive));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        private async Task EnsureConnectionAsync(CancellationToken ct = default)
        {
            if (_conn == null)
            {
                _logger.LogInformation("Creando nueva conexión SQLite en {DbPath} y tablas si no existen.", _dbPath);
                _conn = new SQLiteAsyncConnection(_dbPath);
                await _conn.CreateTableAsync<Cliente>().ConfigureAwait(false);
                await _conn.CreateTableAsync<Servicio>().ConfigureAwait(false);
                await _conn.CreateTableAsync<SyncQueue>().ConfigureAwait(false);
                await _conn.CreateTableAsync<Instalador>().ConfigureAwait(false);
                _logger.LogInformation("Conexión SQLite y tablas aseguradas.");
            }
        }

        private static string GetOrCreateDeviceId()
        {
            const string key = "AppInstanceDeviceId";
            if (!Preferences.ContainsKey(key))
            {
                Preferences.Set(key, Guid.NewGuid().ToString());
            }
            return Preferences.Get(key, string.Empty);
        }

        internal async Task RegistrarEnSyncQueueAsync(string tabla, string tipo, string entidadId, object datos, CancellationToken ct = default)
        {
            await EnsureConnectionAsync(ct).ConfigureAwait(false);

            var entry = new SyncQueue
            {
                SyncId = Guid.NewGuid(),
                DeviceId = GetOrCreateDeviceId(),
                Tabla = tabla,
                TipoCambio = tipo,
                EntidadId = entidadId,
                DatosJson = JsonSerializer.Serialize(datos),
                Timestamp = DateTime.UtcNow,
                ProcesadoPor = GetOrCreateDeviceId()
            };

            try
            {
                await _conn.InsertAsync(entry).ConfigureAwait(false);
                _logger.LogInformation("REGISTRADO EN SYNCQUEUE LOCAL: SyncId={SyncId}, Tabla={Tabla}, Tipo={TipoCambio}, EntidadId={EntidadId}, DeviceIdOrigen={OrigenDeviceId}, ProcesadoPor={ProcesadoPor}",
                    entry.SyncId, entry.Tabla, entry.TipoCambio, entry.EntidadId, entry.DeviceId, entry.ProcesadoPor);
            }
            catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
            {
                _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
            }
        }

        public async Task SubirUltimaVersionBaseDeDatosAsync(CancellationToken ct = default)
        {
            await CerrarConexionAsync().ConfigureAwait(false);

            try
            {
                using var dbStream = File.OpenRead(_dbPath);
                await _oneDrive.UploadFileAsync($"{RemoteFolder}/{Path.GetFileName(_dbPath)}", dbStream).ConfigureAwait(false);
                _logger.LogInformation("Base de datos SQLite subida correctamente a OneDrive.");
                _subidaBaseDatosPendiente = false; // Subida exitosa, limpia el flag
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al subir la base de datos SQLite a OneDrive.");
                _subidaBaseDatosPendiente = true; // Marca subida pendiente
            }
            finally
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
            }
        }
        public async Task IntentarSubidaPendienteBaseDeDatosAsync(CancellationToken ct = default)
        {
            if (_subidaBaseDatosPendiente)
            {
                await SubirUltimaVersionBaseDeDatosAsync(ct).ConfigureAwait(false);
            }
        }


        // --- BORRADO LÓGICO ROBUSTO ---
        public async Task<int> EliminarClienteAsync(Cliente c, CancellationToken ct = default)
        {
            if (c == null) throw new ArgumentNullException(nameof(c), "El cliente no puede ser null.");
            await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            int rowsAffected = 0;
            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                c.IsDeleted = true;
                c.FechaModificacionEliminado = DateTime.UtcNow;
                try
                {
                    rowsAffected = await _conn.UpdateAsync(c).ConfigureAwait(false);
                    if (rowsAffected > 0)
                    {
                        await RegistrarEnSyncQueueAsync("Cliente", "Delete", c.Id.ToString(), c, ct).ConfigureAwait(false);
                    }
                }
                catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                {
                    _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                    throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
            await SubirUltimaVersionBaseDeDatosAsync(ct).ConfigureAwait(false);
            return rowsAffected;
        }

        public async Task<int> EliminarServicioAsync(Servicio s, CancellationToken ct = default)
        {
            if (s == null) throw new ArgumentNullException(nameof(s), "El servicio no puede ser null.");
            await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            int rowsAffected = 0;
            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                s.IsDeleted = true;
                s.FechaModificacionEliminado = DateTime.UtcNow;
                try
                {
                    rowsAffected = await _conn.UpdateAsync(s).ConfigureAwait(false);
                    if (rowsAffected > 0)
                    {
                        await RegistrarEnSyncQueueAsync("Servicio", "Delete", s.Id.ToString(), s, ct).ConfigureAwait(false);
                    }
                }
                catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                {
                    _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                    throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
            await SubirUltimaVersionBaseDeDatosAsync(ct).ConfigureAwait(false);
            return rowsAffected;
        }
        // --- FIN BORRADO LÓGICO ---

        // --- SINCRONIZACIÓN ROBUSTA ---
        public async Task InsertarOActualizarClienteAsync(Cliente remoto, CancellationToken ct = default, bool semaphoreAlreadyAcquired = false)
        {
            if (remoto == null) return;
            if (!semaphoreAlreadyAcquired) await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                var local = await _conn.FindAsync<Cliente>(remoto.Id).ConfigureAwait(false);
                if (local == null)
                {
                    try
                    {
                        await _conn.InsertAsync(remoto).ConfigureAwait(false);
                    }
                    catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                    {
                        _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                        throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                    }
                }
                else
                {
                    if (remoto.IsDeleted && (!local.IsDeleted || remoto.FechaModificacionEliminado > local.FechaModificacionEliminado))
                    {
                        local.IsDeleted = true;
                        local.FechaModificacionEliminado = remoto.FechaModificacionEliminado;
                        try
                        {
                            await _conn.UpdateAsync(local).ConfigureAwait(false);
                        }
                        catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                        {
                            _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                            throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                        }
                        return;
                    }
                    if (local.IsDeleted && local.FechaModificacionEliminado >= remoto.FechaModificacionEliminado)
                    {
                        return;
                    }

                    bool modificado = false;
                    if (remoto.FechaModificacionNombre > local.FechaModificacionNombre) { local.NombreCliente = remoto.NombreCliente; local.FechaModificacionNombre = remoto.FechaModificacionNombre; modificado = true; }
                    if (remoto.FechaModificacionDireccion > local.FechaModificacionDireccion) { local.Direccion = remoto.Direccion; local.FechaModificacionDireccion = remoto.FechaModificacionDireccion; modificado = true; }
                    if (remoto.FechaModificacionEmail > local.FechaModificacionEmail) { local.Email = remoto.Email; local.FechaModificacionEmail = remoto.FechaModificacionEmail; modificado = true; }
                    if (remoto.FechaModificacionTelefono > local.FechaModificacionTelefono) { local.Telefono = remoto.Telefono; local.FechaModificacionTelefono = remoto.FechaModificacionTelefono; modificado = true; }
                    if (modificado)
                    {
                        local.IsSynced = true;
                        try
                        {
                            await _conn.UpdateAsync(local).ConfigureAwait(false);
                        }
                        catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                        {
                            _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                            throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                        }
                    }
                }
            }
            finally
            {
                if (!semaphoreAlreadyAcquired) _dbSemaphore.Release();
            }
        }

        public async Task InsertarOActualizarServicioAsync(Servicio remoto, CancellationToken ct = default, bool semaphoreAlreadyAcquired = false)
        {
            if (remoto == null) return;
            if (!semaphoreAlreadyAcquired) await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                var local = await _conn.FindAsync<Servicio>(remoto.Id).ConfigureAwait(false);
                if (local == null)
                {
                    try
                    {
                        await _conn.InsertAsync(remoto).ConfigureAwait(false);
                    }
                    catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                    {
                        _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                        throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                    }
                }
                else
                {
                    if (remoto.IsDeleted && (!local.IsDeleted || remoto.FechaModificacionEliminado > local.FechaModificacionEliminado))
                    {
                        local.IsDeleted = true;
                        local.FechaModificacionEliminado = remoto.FechaModificacionEliminado;
                        try
                        {
                            await _conn.UpdateAsync(local).ConfigureAwait(false);
                        }
                        catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                        {
                            _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                            throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                        }
                        return;
                    }
                    if (local.IsDeleted && local.FechaModificacionEliminado >= remoto.FechaModificacionEliminado)
                    {
                        return;
                    }

                    bool modificado = false;
                    if (remoto.FechaModificacionFecha > local.FechaModificacionFecha) { local.Fecha = remoto.Fecha; local.FechaModificacionFecha = remoto.FechaModificacionFecha; modificado = true; }
                    if (remoto.FechaModificacionTipoServicio > local.FechaModificacionTipoServicio) { local.TipoServicio = remoto.TipoServicio; local.FechaModificacionTipoServicio = remoto.FechaModificacionTipoServicio; modificado = true; }
                    if (remoto.FechaModificacionTipoInstalacion > local.FechaModificacionTipoInstalacion) { local.TipoInstalacion = remoto.TipoInstalacion; local.FechaModificacionTipoInstalacion = remoto.FechaModificacionTipoInstalacion; modificado = true; }
                    if (remoto.FechaModificacionFuenteCalor > local.FechaModificacionFuenteCalor) { local.FuenteCalor = remoto.FuenteCalor; local.FechaModificacionFuenteCalor = remoto.FechaModificacionFuenteCalor; modificado = true; }
                    if (remoto.FechaModificacionValorPh > local.FechaModificacionValorPh) { local.ValorPh = remoto.ValorPh; local.FechaModificacionValorPh = remoto.FechaModificacionValorPh; modificado = true; }
                    if (remoto.FechaModificacionValorConductividad > local.FechaModificacionValorConductividad) { local.ValorConductividad = remoto.ValorConductividad; local.FechaModificacionValorConductividad = remoto.FechaModificacionValorConductividad; modificado = true; }
                    if (remoto.FechaModificacionValorConcentracion > local.FechaModificacionValorConcentracion) { local.ValorConcentracion = remoto.ValorConcentracion; local.FechaModificacionValorConcentracion = remoto.FechaModificacionValorConcentracion; modificado = true; }
                    if (remoto.FechaModificacionValorTurbidez > local.FechaModificacionValorTurbidez) { local.ValorTurbidez = remoto.ValorTurbidez; local.FechaModificacionValorTurbidez = remoto.FechaModificacionValorTurbidez; modificado = true; }
                    if (remoto.FechaModificacionFotoPhUrl > local.FechaModificacionFotoPhUrl) { local.FotoPhUrl = remoto.FotoPhUrl; local.FechaModificacionFotoPhUrl = remoto.FechaModificacionFotoPhUrl; modificado = true; }
                    if (remoto.FechaModificacionFotoConductividadUrl > local.FechaModificacionFotoConductividadUrl) { local.FotoConductividadUrl = remoto.FotoConductividadUrl; local.FechaModificacionFotoConductividadUrl = remoto.FechaModificacionFotoConductividadUrl; modificado = true; }
                    if (remoto.FechaModificacionFotoConcentracionUrl > local.FechaModificacionFotoConcentracionUrl) { local.FotoConcentracionUrl = remoto.FotoConcentracionUrl; local.FechaModificacionFotoConcentracionUrl = remoto.FechaModificacionFotoConcentracionUrl; modificado = true; }
                    if (remoto.FechaModificacionFotoTurbidezUrl > local.FechaModificacionFotoTurbidezUrl) { local.FotoTurbidezUrl = remoto.FotoTurbidezUrl; local.FechaModificacionFotoTurbidezUrl = remoto.FechaModificacionFotoTurbidezUrl; modificado = true; }
                    if (remoto.FechaModificacionFotoInstalacion1Url > local.FechaModificacionFotoInstalacion1Url) { local.FotoInstalacion1Url = remoto.FotoInstalacion1Url; local.FechaModificacionFotoInstalacion1Url = remoto.FechaModificacionFotoInstalacion1Url; modificado = true; }
                    if (remoto.FechaModificacionFotoInstalacion2Url > local.FechaModificacionFotoInstalacion2Url) { local.FotoInstalacion2Url = remoto.FotoInstalacion2Url; local.FechaModificacionFotoInstalacion2Url = remoto.FechaModificacionFotoInstalacion2Url; modificado = true; }
                    if (remoto.FechaModificacionFotoInstalacion3Url > local.FechaModificacionFotoInstalacion3Url) { local.FotoInstalacion3Url = remoto.FotoInstalacion3Url; local.FechaModificacionFotoInstalacion3Url = remoto.FechaModificacionFotoInstalacion3Url; modificado = true; }
                    if (remoto.FechaModificacionFotoInstalacion4Url > local.FechaModificacionFotoInstalacion4Url) { local.FotoInstalacion4Url = remoto.FotoInstalacion4Url; local.FechaModificacionFotoInstalacion4Url = remoto.FechaModificacionFotoInstalacion4Url; modificado = true; }
                    if (remoto.FechaModificacionFotoInstalacion5Url > local.FechaModificacionFotoInstalacion5Url) { local.FotoInstalacion5Url = remoto.FotoInstalacion5Url; local.FechaModificacionFotoInstalacion5Url = remoto.FechaModificacionFotoInstalacion5Url; modificado = true; }
                    if (remoto.FechaModificacionFotoInstalacion6Url > local.FechaModificacionFotoInstalacion6Url) { local.FotoInstalacion6Url = remoto.FotoInstalacion6Url; local.FechaModificacionFotoInstalacion6Url = remoto.FechaModificacionFotoInstalacion6Url; modificado = true; }
                    if (remoto.FechaModificacionFotoInstalacion7Url > local.FechaModificacionFotoInstalacion7Url) { local.FotoInstalacion7Url = remoto.FotoInstalacion7Url; local.FechaModificacionFotoInstalacion7Url = remoto.FechaModificacionFotoInstalacion7Url; modificado = true; }
                    if (remoto.FechaModificacionFotoInstalacion8Url > local.FechaModificacionFotoInstalacion8Url) { local.FotoInstalacion8Url = remoto.FotoInstalacion8Url; local.FechaModificacionFotoInstalacion8Url = remoto.FechaModificacionFotoInstalacion8Url; modificado = true; }
                    if (remoto.FechaModificacionFotoInstalacion9Url > local.FechaModificacionFotoInstalacion9Url) { local.FotoInstalacion9Url = remoto.FotoInstalacion9Url; local.FechaModificacionFotoInstalacion9Url = remoto.FechaModificacionFotoInstalacion9Url; modificado = true; }
                    if (remoto.FechaModificacionFotoInstalacion10Url > local.FechaModificacionFotoInstalacion10Url) { local.FotoInstalacion10Url = remoto.FotoInstalacion10Url; local.FechaModificacionFotoInstalacion10Url = remoto.FechaModificacionFotoInstalacion10Url; modificado = true; }
                    if (remoto.FechaModificacionEquipamientoUtilizado > local.FechaModificacionEquipamientoUtilizado) { local.EquipamientoUtilizado = remoto.EquipamientoUtilizado; local.FechaModificacionEquipamientoUtilizado = remoto.FechaModificacionEquipamientoUtilizado; modificado = true; }
                    if (remoto.FechaModificacionInhibidoresUtilizados > local.FechaModificacionInhibidoresUtilizados) { local.InhibidoresUtilizados = remoto.InhibidoresUtilizados; local.FechaModificacionInhibidoresUtilizados = remoto.FechaModificacionInhibidoresUtilizados; modificado = true; }
                    if (remoto.FechaModificacionLimpiadoresUtilizados > local.FechaModificacionLimpiadoresUtilizados) { local.LimpiadoresUtilizados = remoto.LimpiadoresUtilizados; local.FechaModificacionLimpiadoresUtilizados = remoto.FechaModificacionLimpiadoresUtilizados; modificado = true; }
                    if (remoto.FechaModificacionBiocidasUtilizados > local.FechaModificacionBiocidasUtilizados) { local.BiocidasUtilizados = remoto.BiocidasUtilizados; local.FechaModificacionBiocidasUtilizados = remoto.FechaModificacionBiocidasUtilizados; modificado = true; }
                    if (remoto.FechaModificacionAnticongelantesUtilizados > local.FechaModificacionAnticongelantesUtilizados) { local.AnticongelantesUtilizados = remoto.AnticongelantesUtilizados; local.FechaModificacionAnticongelantesUtilizados = remoto.FechaModificacionAnticongelantesUtilizados; modificado = true; }
                    if (remoto.FechaModificacionAntiguedadInstalacion > local.FechaModificacionAntiguedadInstalacion) { local.AntiguedadInstalacion = remoto.AntiguedadInstalacion; local.FechaModificacionAntiguedadInstalacion = remoto.FechaModificacionAntiguedadInstalacion; modificado = true; }
                    if (remoto.FechaModificacionAntiguedadAparatoProduccion > local.FechaModificacionAntiguedadAparatoProduccion) { local.AntiguedadAparatoProduccion = remoto.AntiguedadAparatoProduccion; local.FechaModificacionAntiguedadAparatoProduccion = remoto.FechaModificacionAntiguedadAparatoProduccion; modificado = true; }
                    if (remoto.FechaModificacionModelo > local.FechaModificacionModelo) { local.Modelo = remoto.Modelo; local.FechaModificacionModelo = remoto.FechaModificacionModelo; modificado = true; }
                    if (remoto.FechaModificacionMarca > local.FechaModificacionMarca) { local.Marca = remoto.Marca; local.FechaModificacionMarca = remoto.FechaModificacionMarca; modificado = true; }
                    if (remoto.FechaModificacionUltimaRevision > local.FechaModificacionUltimaRevision) { local.UltimaRevision = remoto.UltimaRevision; local.FechaModificacionUltimaRevision = remoto.FechaModificacionUltimaRevision; modificado = true; }
                    if (remoto.FechaModificacionComentariosInstalador > local.FechaModificacionComentariosInstalador) { local.ComentariosInstalador = remoto.ComentariosInstalador; local.FechaModificacionComentariosInstalador = remoto.FechaModificacionComentariosInstalador; modificado = true; }
                    if (remoto.FechaModificacionComentarios > local.FechaModificacionComentarios) { local.Comentarios = remoto.Comentarios; local.FechaModificacionComentarios = remoto.FechaModificacionComentarios; modificado = true; }
                    if (modificado)
                    {
                        local.IsSynced = true;
                        try
                        {
                            await _conn.UpdateAsync(local).ConfigureAwait(false);
                        }
                        catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                        {
                            _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                            throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                        }
                    }
                }
            }
            finally
            {
                if (!semaphoreAlreadyAcquired) _dbSemaphore.Release();
            }
        }

        // --- MÉTODOS PÚBLICOS DE SINCRONIZACIÓN ---
        public async Task SubirSyncQueueAsync(CancellationToken ct = default)
        {
            await EnsureConnectionAsync(ct).ConfigureAwait(false);
            _logger.LogInformation("[SUBIR SYNCQUEUE] Iniciando...");

            List<SyncQueue> colaOneDrive = new List<SyncQueue>();
            bool descargaOneDriveConsideradaExitosa = false;

            try
            {
                _logger.LogInformation("[SUBIR SYNCQUEUE] Descargando syncqueue.json de OneDrive...");
                using var remoteStream = await _oneDrive.DescargarArchivoAsync("syncqueue.json", ct).ConfigureAwait(false);

                if (remoteStream != null && remoteStream.Length == 0)
                {
                    descargaOneDriveConsideradaExitosa = true;
                }
                else if (remoteStream != null)
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    colaOneDrive = await JsonSerializer.DeserializeAsync<List<SyncQueue>>(remoteStream, options, cancellationToken: ct).ConfigureAwait(false)
                                   ?? new List<SyncQueue>();
                    descargaOneDriveConsideradaExitosa = true;
                }
                else
                {
                    descargaOneDriveConsideradaExitosa = true;
                }
            }
            catch (JsonException jsonEx)
            {
                _logger.LogError(jsonEx, "[SUBIR SYNCQUEUE] Error CRÍTICO de formato JSON al deserializar syncqueue.json de OneDrive. NO SE SUBIRÁ para evitar pérdida de datos.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SUBIR SYNCQUEUE] Error CRÍTICO general al descargar syncqueue.json de OneDrive. NO SE SUBIRÁ.");
                return;
            }

            if (!descargaOneDriveConsideradaExitosa)
            {
                _logger.LogWarning("[SUBIR SYNCQUEUE] La descarga de syncqueue.json no fue considerada exitosa. Se omite la subida.");
                return;
            }

            var colaLocal = await _conn.Table<SyncQueue>().ToListAsync().ConfigureAwait(false);
            var dictFusion = colaOneDrive.ToDictionary(x => x.SyncId);

            foreach (var localEntry in colaLocal)
            {
                if (dictFusion.TryGetValue(localEntry.SyncId, out var remoteEntry))
                {
                    var procesadoPorUnion = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (!string.IsNullOrEmpty(remoteEntry.ProcesadoPor))
                        foreach (var id in remoteEntry.ProcesadoPor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            procesadoPorUnion.Add(id);
                    if (!string.IsNullOrEmpty(localEntry.ProcesadoPor))
                        foreach (var id in localEntry.ProcesadoPor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            procesadoPorUnion.Add(id);

                    string nuevoProcesadoPor = string.Join(",", procesadoPorUnion);
                    if (remoteEntry.ProcesadoPor != nuevoProcesadoPor)
                    {
                        remoteEntry.ProcesadoPor = nuevoProcesadoPor;
                    }

                    if (localEntry.Timestamp > remoteEntry.Timestamp)
                    {
                        remoteEntry.DatosJson = localEntry.DatosJson;
                        remoteEntry.TipoCambio = localEntry.TipoCambio;
                        remoteEntry.Tabla = localEntry.Tabla;
                        remoteEntry.EntidadId = localEntry.EntidadId;
                        remoteEntry.Timestamp = localEntry.Timestamp;
                        remoteEntry.DeviceId = localEntry.DeviceId;
                    }
                }
                else
                {
                    dictFusion[localEntry.SyncId] = localEntry;
                }
            }

            int minProcesadoPor = Preferences.Get("SyncMinProcessedBy", 2);
            DateTime limiteAntiguedad = DateTime.UtcNow.AddDays(-Preferences.Get("SyncMaxAgeDays", 30));

            var listaFinalParaSubir = dictFusion.Values
                .Where(sqEntry =>
                    (sqEntry.ProcesadoPor?.Split(',', StringSplitOptions.RemoveEmptyEntries).Length ?? 0) < minProcesadoPor ||
                    sqEntry.Timestamp > limiteAntiguedad)
                .ToList();

            try
            {
                if (listaFinalParaSubir.Any() || (colaOneDrive.Any() && !listaFinalParaSubir.Any()))
                {
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
                    byte[] utf8Bytes = JsonSerializer.SerializeToUtf8Bytes(listaFinalParaSubir, jsonOptions);
                    using var streamParaSubir = new MemoryStream(utf8Bytes);
                    await _oneDrive.UploadFileAsync($"{RemoteFolder}/syncqueue.json", streamParaSubir).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SUBIR SYNCQUEUE] Error al serializar o subir la listaFinal de SyncQueue a OneDrive.");
            }
        }

        public async Task SincronizarDispositivoNuevoAsync(CancellationToken ct = default)
        {
            _logger.LogInformation("[PROCESAR SYNCQUEUE REMOTA] Iniciando...");
            List<SyncQueue> cambiosDeOneDrive;
            try
            {
                cambiosDeOneDrive = await _oneDrive.DescargarSyncQueueDesdeOneDriveAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[PROCESAR SYNCQUEUE REMOTA] Fallo al descargar SyncQueueDesdeOneDriveAsync. Abortando.");
                return;
            }

            if (cambiosDeOneDrive == null || !cambiosDeOneDrive.Any())
            {
                return;
            }

            var currentDeviceId = GetOrCreateDeviceId();

            await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);

                foreach (var cambioRemoto in cambiosDeOneDrive)
                {
                    if (ct.IsCancellationRequested)
                        break;

                    if (!string.Equals(cambioRemoto.DeviceId, currentDeviceId, StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            switch (cambioRemoto.Tabla)
                            {
                                case "Cliente":
                                    var cliente = JsonSerializer.Deserialize<Cliente>(cambioRemoto.DatosJson);
                                    if (cliente != null)
                                        await InsertarOActualizarClienteAsync(cliente, ct, semaphoreAlreadyAcquired: true).ConfigureAwait(false);
                                    break;
                                case "Servicio":
                                    var servicio = JsonSerializer.Deserialize<Servicio>(cambioRemoto.DatosJson);
                                    if (servicio != null)
                                        await InsertarOActualizarServicioAsync(servicio, ct, semaphoreAlreadyAcquired: true).ConfigureAwait(false);
                                    break;
                                case "Instalador":
                                    var instalador = JsonSerializer.Deserialize<Instalador>(cambioRemoto.DatosJson);
                                    if (instalador != null)
                                        await GuardarInstaladorAsync(instalador, ct, semaphoreAlreadyAcquired: true).ConfigureAwait(false);
                                    break;
                            }
                        }
                        catch { }
                    }

                    var entradaLocalExistente = await _conn.Table<SyncQueue>().FirstOrDefaultAsync(x => x.SyncId == cambioRemoto.SyncId).ConfigureAwait(false);
                    if (entradaLocalExistente == null)
                    {
                        await _conn.InsertAsync(cambioRemoto).ConfigureAwait(false);
                    }
                    else
                    {
                        bool esNecesarioActualizarLocal = entradaLocalExistente.ProcesadoPor != cambioRemoto.ProcesadoPor ||
                                                          (cambioRemoto.Timestamp > entradaLocalExistente.Timestamp && string.Equals(cambioRemoto.DeviceId, entradaLocalExistente.DeviceId, StringComparison.OrdinalIgnoreCase));

                        if (esNecesarioActualizarLocal)
                        {
                            entradaLocalExistente.ProcesadoPor = cambioRemoto.ProcesadoPor;
                            if (cambioRemoto.Timestamp > entradaLocalExistente.Timestamp && string.Equals(cambioRemoto.DeviceId, entradaLocalExistente.DeviceId, StringComparison.OrdinalIgnoreCase))
                            {
                                entradaLocalExistente.Timestamp = cambioRemoto.Timestamp;
                                entradaLocalExistente.DatosJson = cambioRemoto.DatosJson;
                                entradaLocalExistente.TipoCambio = cambioRemoto.TipoCambio;
                            }
                            await _conn.UpdateAsync(entradaLocalExistente).ConfigureAwait(false);
                        }
                    }
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        // --- CONSULTAS FILTRADAS ---
        public async Task<List<Cliente>> ObtenerClientesAsync(CancellationToken ct = default)
        {
            await EnsureConnectionAsync(ct).ConfigureAwait(false);
            return await _conn.Table<Cliente>().Where(c => !c.IsDeleted).ToListAsync().ConfigureAwait(false);
        }

        public async Task<Cliente?> ObtenerClientePorIdAsync(int clienteId, CancellationToken ct = default)
        {
            await EnsureConnectionAsync(ct).ConfigureAwait(false);
            var cliente = await _conn.FindAsync<Cliente>(clienteId).ConfigureAwait(false);
            return (cliente != null && !cliente.IsDeleted) ? cliente : null;
        }

        public async Task<List<Servicio>> ObtenerServiciosAsync(int clienteId, CancellationToken ct = default)
        {
            await EnsureConnectionAsync(ct).ConfigureAwait(false);
            return await _conn.Table<Servicio>()
                              .Where(s => s.ClienteId == clienteId && !s.IsDeleted)
                              .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<Servicio>> ObtenerTodosLosServiciosAsync(CancellationToken ct = default)
        {
            await EnsureConnectionAsync(ct).ConfigureAwait(false);
            return await _conn.Table<Servicio>().Where(s => !s.IsDeleted).ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<Cliente>> ObtenerClientesNoSincronizadosAsync(CancellationToken ct = default)
        {
            await EnsureConnectionAsync(ct).ConfigureAwait(false);
            return await _conn.Table<Cliente>().Where(c => !c.IsSynced).ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<Servicio>> ObtenerServiciosNoSincronizadosAsync(CancellationToken ct = default)
        {
            await EnsureConnectionAsync(ct).ConfigureAwait(false);
            return await _conn.Table<Servicio>().Where(s => !s.IsSynced).ToListAsync().ConfigureAwait(false);
        }

        public async Task MarcarClientesComoSincronizadosAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            if (ids == null || !ids.Any()) return;
            await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                foreach (var id in ids)
                {
                    var cliente = await _conn.FindAsync<Cliente>(id).ConfigureAwait(false);
                    if (cliente != null)
                    {
                        cliente.IsSynced = true;
                        await _conn.UpdateAsync(cliente).ConfigureAwait(false);
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
            if (ids == null || !ids.Any()) return;
            await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                foreach (var id in ids)
                {
                    var servicio = await _conn.FindAsync<Servicio>(id).ConfigureAwait(false);
                    if (servicio != null)
                    {
                        servicio.IsSynced = true;
                        await _conn.UpdateAsync(servicio).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
        }

        public async Task InitAsync(CancellationToken ct = default)
        {
            var dir = Path.GetDirectoryName(_dbPath);
            if (dir != null && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Si la base de datos no existe, restaurar desde OneDrive
            if (!File.Exists(_dbPath))
            {
                try
                {
                    await _oneDrive.RestaurarBaseDeDatosAsync(_dbPath).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al restaurar la base de datos desde OneDrive. Se creará una nueva base local.");
                }
            }

            // Siempre quitar el atributo solo lectura si existe, ANTES de abrir la conexión
            var fileInfo = new FileInfo(_dbPath);
            if (fileInfo.Exists && fileInfo.IsReadOnly)
            {
                try
                {
                    fileInfo.IsReadOnly = false;
                    _logger.LogWarning("El archivo de base de datos tenía el atributo solo lectura. Se ha corregido.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo quitar el atributo solo lectura. Revisa permisos del sistema de archivos.");
                    throw;
                }
            }

            // Solo ahora abrir la conexión
            await EnsureConnectionAsync(ct).ConfigureAwait(false);
        }

        public async Task ActualizarClienteAsync(Cliente cliente, CancellationToken ct = default)
        {
            if (cliente == null) throw new ArgumentNullException(nameof(cliente));
            await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                try
                {
                    await _conn.UpdateAsync(cliente).ConfigureAwait(false);
                    await RegistrarEnSyncQueueAsync("Cliente", "Update", cliente.Id.ToString(), cliente, ct).ConfigureAwait(false);
                }
                catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                {
                    _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                    throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
            await SubirUltimaVersionBaseDeDatosAsync(ct).ConfigureAwait(false);
        }

        public async Task ActualizarServicioAsync(Servicio servicio, CancellationToken ct = default)
        {
            if (servicio == null) throw new ArgumentNullException(nameof(servicio));
            await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                try
                {
                    await _conn.UpdateAsync(servicio).ConfigureAwait(false);
                    await RegistrarEnSyncQueueAsync("Servicio", "Update", servicio.Id.ToString(), servicio, ct).ConfigureAwait(false);
                }
                catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                {
                    _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                    throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
            await SubirUltimaVersionBaseDeDatosAsync(ct).ConfigureAwait(false);
        }

        public async Task<int> GuardarClienteAsync(Cliente c, CancellationToken ct = default)
        {
            if (c == null) throw new ArgumentNullException(nameof(c), "El cliente no puede ser null.");
            await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                string tipoCambio;
                try
                {
                    if (c.Id == 0)
                    {
                        await _conn.InsertAsync(c).ConfigureAwait(false);
                        tipoCambio = "Insert";
                    }
                    else
                    {
                        await _conn.UpdateAsync(c).ConfigureAwait(false);
                        tipoCambio = "Update";
                    }
                    await RegistrarEnSyncQueueAsync("Cliente", tipoCambio, c.Id.ToString(), c, ct).ConfigureAwait(false);
                    return c.Id;
                }
                catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                {
                    _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                    throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
            await SubirUltimaVersionBaseDeDatosAsync(ct).ConfigureAwait(false);
        }

        public async Task<int> GuardarServicioAsync(Servicio s, CancellationToken ct = default)
        {
            if (s == null) throw new ArgumentNullException(nameof(s), "El servicio no puede ser null.");
            await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                string tipoCambio;
                try
                {
                    if (s.Id == 0)
                    {
                        await _conn.InsertAsync(s).ConfigureAwait(false);
                        tipoCambio = "Insert";
                    }
                    else
                    {
                        await _conn.UpdateAsync(s).ConfigureAwait(false);
                        tipoCambio = "Update";
                    }
                    await RegistrarEnSyncQueueAsync("Servicio", tipoCambio, s.Id.ToString(), s, ct).ConfigureAwait(false);
                    return s.Id;
                }
                catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                {
                    _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                    throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                }
            }
            finally
            {
                _dbSemaphore.Release();
            }
            await SubirUltimaVersionBaseDeDatosAsync(ct).ConfigureAwait(false);
        }

        public async Task CerrarConexionAsync()
        {
            if (_conn != null)
            {
                await _conn.CloseAsync().ConfigureAwait(false);
                _conn = null;
            }
        }

        public async Task GuardarInstaladorAsync(Instalador instalador, CancellationToken ct = default, bool semaphoreAlreadyAcquired = false)
        {
            if (instalador == null) throw new ArgumentNullException(nameof(instalador));

            if (!semaphoreAlreadyAcquired)
            {
                await _dbSemaphore.WaitAsync(ct).ConfigureAwait(false);
            }

            try
            {
                await EnsureConnectionAsync(ct).ConfigureAwait(false);
                var existente = await _conn.Table<Instalador>().FirstOrDefaultAsync().ConfigureAwait(false);
                string tipoCambio;

                try
                {
                    if (existente == null)
                    {
                        await _conn.InsertAsync(instalador).ConfigureAwait(false);
                        tipoCambio = "Insert";
                    }
                    else
                    {
                        instalador.Id = existente.Id;
                        await _conn.UpdateAsync(instalador).ConfigureAwait(false);
                        tipoCambio = "Update";
                    }
                    await RegistrarEnSyncQueueAsync("Instalador", tipoCambio, instalador.Id.ToString(), instalador, ct).ConfigureAwait(false);
                }
                catch (SQLiteException ex) when (ex.Message.Contains("readonly"))
                {
                    _logger.LogError(ex, "Intento de escritura fallido: la base de datos está en modo solo lectura.");
                    throw new InvalidOperationException("La base de datos está en modo solo lectura. No se pueden guardar cambios.", ex);
                }
            }
            finally
            {
                if (!semaphoreAlreadyAcquired)
                {
                    _dbSemaphore.Release();
                }
            }
            await SubirUltimaVersionBaseDeDatosAsync(ct).ConfigureAwait(false);
        }

        public async Task<Instalador?> ObtenerInstaladorAsync(CancellationToken ct = default)
        {
            await EnsureConnectionAsync(ct).ConfigureAwait(false);
            return await _conn.Table<Instalador>().FirstOrDefaultAsync().ConfigureAwait(false);
        }
    }
}