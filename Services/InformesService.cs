using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Models;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class InformesService
    {
        private readonly DatabaseService _databaseService;

        public InformesService(DatabaseService databaseService)
        {
            _databaseService = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
        }

        /// <summary>
        /// Genera un informe detallado de un cliente específico
        /// </summary>
        public async Task<bool> GenerarInformeClienteAsync(Page page, int clienteId)
        {
            if (!await PremiumHelper.VerificarAccesoPremiumAsync(page))
            {
                return false;
            }

            try
            {
                var cliente = await _databaseService.ObtenerClientePorIdAsync(clienteId);
                if (cliente == null)
                {
                    await page.DisplayAlert("Error", "Cliente no encontrado", "OK");
                    return false;
                }

                var servicios = await _databaseService.ObtenerServiciosAsync(clienteId);
                var contenidoInforme = GenerarContenidoInformeCliente(cliente, servicios);
                
                await GuardarYCompartirInforme($"Informe_Cliente_{cliente.NombreCliente}_{DateTime.Now:yyyyMMdd}.txt", contenidoInforme);
                
                await page.DisplayAlert("Éxito", "Informe generado correctamente", "OK");
                return true;
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("Error", $"Error al generar informe: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"Error generando informe cliente: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Genera un informe general de todos los servicios
        /// </summary>
        public async Task<bool> GenerarInformeGeneralAsync(Page page)
        {
            if (!await PremiumHelper.VerificarAccesoPremiumAsync(page))
            {
                return false;
            }

            try
            {
                var clientes = await _databaseService.ObtenerClientesAsync();
                var todosLosServicios = await _databaseService.ObtenerTodosLosServiciosAsync();
                
                var contenidoInforme = GenerarContenidoInformeGeneral(clientes, todosLosServicios);
                
                await GuardarYCompartirInforme($"Informe_General_{DateTime.Now:yyyyMMdd}.txt", contenidoInforme);
                
                await page.DisplayAlert("Éxito", "Informe general generado correctamente", "OK");
                return true;
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("Error", $"Error al generar informe: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"Error generando informe general: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Genera un informe de servicios por rango de fechas
        /// </summary>
        public async Task<bool> GenerarInformePorFechasAsync(Page page, DateTime fechaInicio, DateTime fechaFin)
        {
            if (!await PremiumHelper.VerificarAccesoPremiumAsync(page))
            {
                return false;
            }

            try
            {
                var todosLosServicios = await _databaseService.ObtenerTodosLosServiciosAsync();
                var serviciosFiltrados = todosLosServicios
                    .Where(s => s.Fecha >= fechaInicio && s.Fecha <= fechaFin)
                    .OrderBy(s => s.Fecha)
                    .ToList();

                if (!serviciosFiltrados.Any())
                {
                    await page.DisplayAlert("Sin datos", "No se encontraron servicios en el rango de fechas seleccionado", "OK");
                    return false;
                }

                var contenidoInforme = GenerarContenidoInformePorFechas(serviciosFiltrados, fechaInicio, fechaFin);
                
                await GuardarYCompartirInforme($"Informe_Fechas_{fechaInicio:yyyyMMdd}_{fechaFin:yyyyMMdd}.txt", contenidoInforme);
                
                await page.DisplayAlert("Éxito", $"Informe generado con {serviciosFiltrados.Count} servicios", "OK");
                return true;
            }
            catch (Exception ex)
            {
                await page.DisplayAlert("Error", $"Error al generar informe: {ex.Message}", "OK");
                System.Diagnostics.Debug.WriteLine($"Error generando informe por fechas: {ex.Message}");
                return false;
            }
        }

        private string GenerarContenidoInformeCliente(Cliente cliente, List<Servicio> servicios)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=================================================");
            sb.AppendLine("           INFORME DETALLADO DE CLIENTE");
            sb.AppendLine("=================================================");
            sb.AppendLine($"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine();
            
            // Información del cliente
            sb.AppendLine("DATOS DEL CLIENTE:");
            sb.AppendLine($"Nombre: {cliente.NombreCliente}");
            sb.AppendLine($"Dirección: {cliente.Direccion}");
            sb.AppendLine($"Email: {cliente.Email}");
            sb.AppendLine($"Teléfono: {cliente.Telefono}");
            sb.AppendLine();
            
            // Resumen de servicios
            sb.AppendLine($"RESUMEN DE SERVICIOS (Total: {servicios.Count}):");
            sb.AppendLine("-------------------------------------------------");
            
            if (servicios.Any())
            {
                var serviciosPorTipo = servicios.GroupBy(s => s.TipoServicio);
                foreach (var grupo in serviciosPorTipo)
                {
                    sb.AppendLine($"• {grupo.Key}: {grupo.Count()} servicios");
                }
                
                sb.AppendLine();
                sb.AppendLine($"Primer servicio: {servicios.Min(s => s.Fecha):dd/MM/yyyy}");
                sb.AppendLine($"Último servicio: {servicios.Max(s => s.Fecha):dd/MM/yyyy}");
                sb.AppendLine();
                
                // Detalle de cada servicio
                sb.AppendLine("DETALLE DE SERVICIOS:");
                sb.AppendLine("-------------------------------------------------");
                
                foreach (var servicio in servicios.OrderByDescending(s => s.Fecha))
                {
                    sb.AppendLine($"Fecha: {servicio.Fecha:dd/MM/yyyy}");
                    sb.AppendLine($"Tipo de servicio: {servicio.TipoServicio}");
                    sb.AppendLine($"Tipo de instalación: {servicio.TipoInstalacion}");
                    sb.AppendLine($"Fuente de calor: {servicio.FuenteCalor}");
                    
                    if (!string.IsNullOrEmpty(servicio.ValorPh?.ToString()))
                        sb.AppendLine($"pH: {servicio.ValorPh}");
                    if (!string.IsNullOrEmpty(servicio.ValorConductividad?.ToString()))
                        sb.AppendLine($"Conductividad: {servicio.ValorConductividad}");
                    if (!string.IsNullOrEmpty(servicio.ValorConcentracion?.ToString()))
                        sb.AppendLine($"Concentración inhibidor: {servicio.ValorConcentracion}");
                    if (!string.IsNullOrEmpty(servicio.ValorTurbidez))
                        sb.AppendLine($"Turbidez: {servicio.ValorTurbidez}");
                    
                    if (!string.IsNullOrEmpty(servicio.Comentarios))
                        sb.AppendLine($"Comentarios: {servicio.Comentarios}");
                    if (!string.IsNullOrEmpty(servicio.ComentariosInstalador))
                        sb.AppendLine($"Comentarios del instalador: {servicio.ComentariosInstalador}");
                    
                    sb.AppendLine();
                    sb.AppendLine("- - - - - - - - - - - - - - - - - - - - - - - - -");
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("No hay servicios registrados para este cliente.");
            }
            
            sb.AppendLine("=================================================");
            sb.AppendLine("         FIN DEL INFORME");
            sb.AppendLine("=================================================");
            
            return sb.ToString();
        }

        private string GenerarContenidoInformeGeneral(List<Cliente> clientes, List<Servicio> servicios)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=================================================");
            sb.AppendLine("               INFORME GENERAL");
            sb.AppendLine("=================================================");
            sb.AppendLine($"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine();
            
            // Estadísticas generales
            sb.AppendLine("ESTADÍSTICAS GENERALES:");
            sb.AppendLine("-------------------------------------------------");
            sb.AppendLine($"Total de clientes: {clientes.Count}");
            sb.AppendLine($"Total de servicios: {servicios.Count}");
            
            if (servicios.Any())
            {
                sb.AppendLine($"Servicios este mes: {servicios.Count(s => s.Fecha.Month == DateTime.Now.Month && s.Fecha.Year == DateTime.Now.Year)}");
                sb.AppendLine($"Servicios este año: {servicios.Count(s => s.Fecha.Year == DateTime.Now.Year)}");
                sb.AppendLine();
                
                // Servicios por tipo
                sb.AppendLine("SERVICIOS POR TIPO:");
                sb.AppendLine("-------------------------------------------------");
                var serviciosPorTipo = servicios.GroupBy(s => s.TipoServicio);
                foreach (var grupo in serviciosPorTipo.OrderByDescending(g => g.Count()))
                {
                    sb.AppendLine($"• {grupo.Key}: {grupo.Count()} servicios");
                }
                sb.AppendLine();
                
                // Tipos de instalación más comunes
                sb.AppendLine("TIPOS DE INSTALACIÓN:");
                sb.AppendLine("-------------------------------------------------");
                var instalacionesPorTipo = servicios.GroupBy(s => s.TipoInstalacion);
                foreach (var grupo in instalacionesPorTipo.OrderByDescending(g => g.Count()))
                {
                    sb.AppendLine($"• {grupo.Key}: {grupo.Count()} instalaciones");
                }
                sb.AppendLine();
                
                // Clientes más activos
                sb.AppendLine("CLIENTES MÁS ACTIVOS:");
                sb.AppendLine("-------------------------------------------------");
                var serviciosPorCliente = servicios.GroupBy(s => s.ClienteId);
                var clientesActivos = serviciosPorCliente
                    .Select(g => new { 
                        ClienteId = g.Key, 
                        CantidadServicios = g.Count(),
                        Cliente = clientes.FirstOrDefault(c => c.Id == g.Key)
                    })
                    .Where(x => x.Cliente != null)
                    .OrderByDescending(x => x.CantidadServicios)
                    .Take(10);
                
                foreach (var clienteActivo in clientesActivos)
                {
                    sb.AppendLine($"• {clienteActivo.Cliente?.NombreCliente}: {clienteActivo.CantidadServicios} servicios");
                }
                sb.AppendLine();
                
                // Evolución mensual
                sb.AppendLine("EVOLUCIÓN MENSUAL (ÚLTIMOS 12 MESES):");
                sb.AppendLine("-------------------------------------------------");
                var fechaInicio = DateTime.Now.AddMonths(-12);
                var serviciosPorMes = servicios
                    .Where(s => s.Fecha >= fechaInicio)
                    .GroupBy(s => new { s.Fecha.Year, s.Fecha.Month })
                    .OrderBy(g => g.Key.Year)
                    .ThenBy(g => g.Key.Month);
                
                foreach (var mes in serviciosPorMes)
                {
                    var fechaMes = new DateTime(mes.Key.Year, mes.Key.Month, 1);
                    sb.AppendLine($"• {fechaMes:MM/yyyy}: {mes.Count()} servicios");
                }
            }
            
            sb.AppendLine();
            sb.AppendLine("=================================================");
            sb.AppendLine("         FIN DEL INFORME");
            sb.AppendLine("=================================================");
            
            return sb.ToString();
        }

        private string GenerarContenidoInformePorFechas(List<Servicio> servicios, DateTime fechaInicio, DateTime fechaFin)
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("=================================================");
            sb.AppendLine("           INFORME POR FECHAS");
            sb.AppendLine("=================================================");
            sb.AppendLine($"Generado el: {DateTime.Now:dd/MM/yyyy HH:mm}");
            sb.AppendLine($"Período: {fechaInicio:dd/MM/yyyy} - {fechaFin:dd/MM/yyyy}");
            sb.AppendLine();
            
            sb.AppendLine($"SERVICIOS REALIZADOS: {servicios.Count}");
            sb.AppendLine("-------------------------------------------------");
            
            foreach (var servicio in servicios)
            {
                sb.AppendLine($"Fecha: {servicio.Fecha:dd/MM/yyyy}");
                sb.AppendLine($"Cliente ID: {servicio.ClienteId}");
                sb.AppendLine($"Tipo: {servicio.TipoServicio}");
                sb.AppendLine($"Instalación: {servicio.TipoInstalacion}");
                
                if (!string.IsNullOrEmpty(servicio.Comentarios))
                    sb.AppendLine($"Comentarios: {servicio.Comentarios}");
                
                sb.AppendLine();
                sb.AppendLine("- - - - - - - - - - - - - - - - - - - - - - - - -");
                sb.AppendLine();
            }
            
            sb.AppendLine("=================================================");
            sb.AppendLine("         FIN DEL INFORME");
            sb.AppendLine("=================================================");
            
            return sb.ToString();
        }

        private async Task GuardarYCompartirInforme(string nombreArchivo, string contenido)
        {
            try
            {
                var filePath = Path.Combine(FileSystem.CacheDirectory, nombreArchivo);
                await File.WriteAllTextAsync(filePath, contenido, Encoding.UTF8);
                
                // Abrir el archivo con la aplicación predeterminada del sistema
                await Launcher.Default.OpenAsync(new OpenFileRequest
                {
                    File = new ReadOnlyFile(filePath),
                    Title = "Abrir informe"
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error guardando/abriendo archivo: {ex.Message}");
                throw;
            }
        }
    }
}
