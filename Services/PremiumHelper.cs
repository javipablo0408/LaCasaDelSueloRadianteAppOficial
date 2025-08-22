using System;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using LaCasaDelSueloRadianteApp.Pages;

namespace LaCasaDelSueloRadianteApp.Services
{
    public static class PremiumHelper
    {
        private static PremiumCodeService? _premiumService;
        
        private static PremiumCodeService PremiumService => 
            _premiumService ??= new PremiumCodeService();

        /// <summary>
        /// Obtiene la instancia compartida del PremiumCodeService
        /// </summary>
        /// <returns>La instancia del servicio premium</returns>
        public static PremiumCodeService GetPremiumService()
        {
            return PremiumService;
        }

        /// <summary>
        /// Verifica si el usuario tiene acceso premium y muestra un diálogo de bloqueo si no lo tiene
        /// </summary>
        /// <param name="page">La página desde la cual se hace la verificación</param>
        /// <returns>True si tiene acceso premium, False si no</returns>
        public static async Task<bool> VerificarAccesoPremiumAsync(Page page)
        {
            if (PremiumService.TieneFuncionalidadPremium())
            {
                return true;
            }

            await MostrarDialogoBloqueo(page);
            return false;
        }

        /// <summary>
        /// Muestra un diálogo informando que la función está bloqueada
        /// </summary>
        /// <param name="page">La página desde la cual mostrar el diálogo</param>
        public static async Task MostrarDialogoBloqueo(Page page)
        {
            var mensaje = PremiumService.ObtenerMensajeDesbloqueo();
            
            var respuesta = await page.DisplayAlert(
                "Función Premium", 
                mensaje, 
                "Ir a Premium", 
                "Cancelar"
            );

            if (respuesta)
            {
                await page.Navigation.PushAsync(new CodigoPremiumPage());
            }
        }

        /// <summary>
        /// Ejecuta una acción solo si el usuario tiene acceso premium
        /// </summary>
        /// <param name="page">La página desde la cual se ejecuta</param>
        /// <param name="accion">La acción a ejecutar si tiene acceso premium</param>
        public static async Task EjecutarSiEsPremiumAsync(Page page, Func<Task> accion)
        {
            if (await VerificarAccesoPremiumAsync(page))
            {
                await accion();
            }
        }

        /// <summary>
        /// Ejecuta una acción solo si el usuario tiene acceso premium (versión síncrona)
        /// </summary>
        /// <param name="page">La página desde la cual se ejecuta</param>
        /// <param name="accion">La acción a ejecutar si tiene acceso premium</param>
        public static async Task EjecutarSiEsPremiumAsync(Page page, Action accion)
        {
            if (await VerificarAccesoPremiumAsync(page))
            {
                accion();
            }
        }

        /// <summary>
        /// Obtiene el estado actual del premium
        /// </summary>
        public static EstadoPremium ObtenerEstadoPremium()
        {
            return PremiumService.ObtenerEstadoPremium();
        }

        /// <summary>
        /// Verifica si tiene funcionalidad premium (sin mostrar diálogos)
        /// </summary>
        public static bool TieneFuncionalidadPremium()
        {
            return PremiumService.TieneFuncionalidadPremium();
        }
    }
}
