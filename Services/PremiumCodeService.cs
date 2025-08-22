using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Maui.Storage;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class PremiumCodeService
    {
        private readonly HttpClient _httpClient;
        private const string BASE_URL = "https://lacasadelsueloradiante.es/wp-json/premium/v1";
        private const string PREF_KEY_CODIGO = "PremiumCode";
        private const string PREF_KEY_FECHA_EXPIRACION = "PremiumCodeExpiration";
        private const string PREF_KEY_ES_ACTIVO = "PremiumCodeActive";
        private const string PREF_KEY_DEVICE_ID = "PremiumDeviceId";

        public PremiumCodeService()
        {
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        public EstadoPremium ObtenerEstadoPremium()
        {
            var esActivo = Preferences.Get(PREF_KEY_ES_ACTIVO, false);
            var fechaExpiracionStr = Preferences.Get(PREF_KEY_FECHA_EXPIRACION, string.Empty);
            var codigo = Preferences.Get(PREF_KEY_CODIGO, string.Empty);

            System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] ObtenerEstadoPremium - EsActivo: {esActivo}");
            System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] ObtenerEstadoPremium - Fecha: {fechaExpiracionStr}");
            System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] ObtenerEstadoPremium - Código: {codigo}");

            if (!esActivo || string.IsNullOrEmpty(fechaExpiracionStr))
            {
                System.Diagnostics.Debug.WriteLine("[PremiumCodeService] Estado no activo o fecha vacía");
                return new EstadoPremium
                {
                    EsActivo = false,
                    MensajeEstado = "Sin código premium activo"
                };
            }

            if (DateTime.TryParse(fechaExpiracionStr, out var fechaExpiracion))
            {
                System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Fecha parseada: {fechaExpiracion}, Actual: {DateTime.Now}");
                
                if (fechaExpiracion <= DateTime.Now)
                {
                    System.Diagnostics.Debug.WriteLine("[PremiumCodeService] Código expirado");
                    // El código ha expirado
                    LimpiarCodigoPremium();
                    return new EstadoPremium
                    {
                        EsActivo = false,
                        MensajeEstado = "Código premium expirado"
                    };
                }

                System.Diagnostics.Debug.WriteLine("[PremiumCodeService] Código activo y válido");
                return new EstadoPremium
                {
                    EsActivo = true,
                    FechaExpiracion = fechaExpiracion,
                    Codigo = codigo
                };
            }

            System.Diagnostics.Debug.WriteLine("[PremiumCodeService] Error parseando fecha");
            return new EstadoPremium
            {
                EsActivo = false,
                MensajeEstado = "Error en la configuración del código"
            };
        }

        public async Task<ResultadoValidacion> ValidarCodigoAsync(string codigo)
        {
            try
            {
                var deviceId = ObtenerOCrearDeviceId();
                
                var requestData = new
                {
                    code = codigo,
                    device = deviceId
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BASE_URL}/validar", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Respuesta del servidor: {responseContent}");
                System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Status code: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<ApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Result Status: {result?.Status}");
                    System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Result Message: {result?.Message}");
                    System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Result ExpiresAt: {result?.ExpiresAt}");

                    if (result != null && result.Status == "success")
                    {
                        // Si no hay fecha de expiración del servidor, usar 30 días por defecto
                        var fechaExpiracion = result.ExpiresAt;
                        if (string.IsNullOrEmpty(fechaExpiracion))
                        {
                            fechaExpiracion = DateTime.Now.AddDays(30).ToString("yyyy-MM-ddTHH:mm:ss");
                            System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Fecha de expiración vacía del servidor, usando fecha por defecto: {fechaExpiracion}");
                        }
                        
                        // Guardar el código como válido
                        GuardarCodigoPremium(codigo, fechaExpiracion);
                        
                        return new ResultadoValidacion
                        {
                            EsExitoso = true,
                            Mensaje = result.Message
                        };
                    }
                    else
                    {
                        return new ResultadoValidacion
                        {
                            EsExitoso = false,
                            Mensaje = result?.Message ?? "Error desconocido"
                        };
                    }
                }
                else
                {
                    var errorResult = JsonSerializer.Deserialize<ApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    return new ResultadoValidacion
                    {
                        EsExitoso = false,
                        Mensaje = errorResult?.Message ?? $"Error del servidor: {response.StatusCode}"
                    };
                }
            }
            catch (TaskCanceledException)
            {
                return new ResultadoValidacion
                {
                    EsExitoso = false,
                    Mensaje = "Tiempo de espera agotado. Verifica tu conexión a internet."
                };
            }
            catch (HttpRequestException)
            {
                return new ResultadoValidacion
                {
                    EsExitoso = false,
                    Mensaje = "Error de conexión. Verifica tu conexión a internet."
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error validando código: {ex.Message}");
                return new ResultadoValidacion
                {
                    EsExitoso = false,
                    Mensaje = "Error inesperado. Inténtalo de nuevo."
                };
            }
        }

        public async Task VerificarEstadoRemoroAsync()
        {
            var estadoActual = ObtenerEstadoPremium();
            
            if (!estadoActual.EsActivo || string.IsNullOrEmpty(estadoActual.Codigo))
            {
                return; // No hay código activo que verificar
            }

            try
            {
                var requestData = new
                {
                    code = estadoActual.Codigo,
                    check_only = true
                };

                var json = JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{BASE_URL}/validar", content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<ApiResponse>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (result != null && result.Status == "success")
                    {
                        // Actualizar la fecha de expiración si es diferente
                        if (!string.IsNullOrEmpty(result.ExpiresAt))
                        {
                            if (DateTime.TryParse(result.ExpiresAt, out var nuevaFechaExpiracion))
                            {
                                Preferences.Set(PREF_KEY_FECHA_EXPIRACION, nuevaFechaExpiracion.ToString());
                            }
                        }
                    }
                    else if (result != null && result.Status == "error")
                    {
                        // El código ya no es válido (expirado o usado)
                        LimpiarCodigoPremium();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error verificando estado remoto: {ex.Message}");
                // No hacer nada en caso de error de red, mantener el estado local
            }
        }

        public bool TieneFuncionalidadPremium()
        {
            var estado = ObtenerEstadoPremium();
            return estado.EsActivo;
        }

        public string ObtenerMensajeDesbloqueo()
        {
            return "Desbloquea esta función con un código al realizar un pedido en lacasadelsueloradiante.es";
        }

        private void GuardarCodigoPremium(string codigo, string fechaExpiracionStr)
        {
            System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Guardando código: {codigo}");
            System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Fecha expiración recibida: '{fechaExpiracionStr}'");
            System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Fecha expiración length: {fechaExpiracionStr?.Length ?? 0}");
            System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Fecha expiración is null or empty: {string.IsNullOrEmpty(fechaExpiracionStr)}");
            
            Preferences.Set(PREF_KEY_CODIGO, codigo);
            Preferences.Set(PREF_KEY_ES_ACTIVO, true);
            
            if (DateTime.TryParse(fechaExpiracionStr, out var fechaExpiracion))
            {
                var fechaGuardada = fechaExpiracion.ToString();
                Preferences.Set(PREF_KEY_FECHA_EXPIRACION, fechaGuardada);
                System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Fecha expiración parseada correctamente: {fechaExpiracion}");
                System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Fecha expiración guardada: {fechaGuardada}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] ERROR: No se pudo parsear la fecha: '{fechaExpiracionStr}'");
                
                // Intentar diferentes formatos
                var formatos = new[] { 
                    "yyyy-MM-ddTHH:mm:ssZ", 
                    "yyyy-MM-dd HH:mm:ss", 
                    "yyyy-MM-ddTHH:mm:ss.fffZ",
                    "yyyy-MM-ddTHH:mm:ss",
                    "MM/dd/yyyy HH:mm:ss",
                    "dd/MM/yyyy HH:mm:ss"
                };
                
                foreach (var formato in formatos)
                {
                    if (DateTime.TryParseExact(fechaExpiracionStr, formato, null, System.Globalization.DateTimeStyles.None, out fechaExpiracion))
                    {
                        var fechaGuardada = fechaExpiracion.ToString();
                        Preferences.Set(PREF_KEY_FECHA_EXPIRACION, fechaGuardada);
                        System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Fecha parseada con formato {formato}: {fechaExpiracion}");
                        System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Fecha guardada: {fechaGuardada}");
                        break;
                    }
                }
            }
            
            // Verificar que se guardó correctamente
            var verificacion = ObtenerEstadoPremium();
            System.Diagnostics.Debug.WriteLine($"[PremiumCodeService] Verificación inmediata - EsActivo: {verificacion.EsActivo}");
        }

        private void LimpiarCodigoPremium()
        {
            Preferences.Remove(PREF_KEY_CODIGO);
            Preferences.Remove(PREF_KEY_FECHA_EXPIRACION);
            Preferences.Set(PREF_KEY_ES_ACTIVO, false);
        }

        private string ObtenerOCrearDeviceId()
        {
            var deviceId = Preferences.Get(PREF_KEY_DEVICE_ID, string.Empty);
            
            if (string.IsNullOrEmpty(deviceId))
            {
                deviceId = Guid.NewGuid().ToString();
                Preferences.Set(PREF_KEY_DEVICE_ID, deviceId);
            }
            
            return deviceId;
        }
    }

    public class EstadoPremium
    {
        public bool EsActivo { get; set; }
        public DateTime FechaExpiracion { get; set; }
        public string Codigo { get; set; } = string.Empty;
        public string MensajeEstado { get; set; } = string.Empty;
    }

    public class ResultadoValidacion
    {
        public bool EsExitoso { get; set; }
        public string Mensaje { get; set; } = string.Empty;
    }

    public class ApiResponse
    {
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string ExpiresAt { get; set; } = string.Empty;
        public bool IsUsed { get; set; }
        public string UsedDevice { get; set; } = string.Empty;
    }
}
