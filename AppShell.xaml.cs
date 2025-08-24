using Microsoft.Maui.Controls;

namespace LaCasaDelSueloRadianteApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Configurar el comportamiento del menú hamburguesa
            ConfigureFlyoutBehavior();

            // Registrar rutas para navegación
            Routing.RegisterRoute(nameof(AgregarPage), typeof(AgregarPage));
            Routing.RegisterRoute(nameof(FullScreenImagePage), typeof(FullScreenImagePage));
            Routing.RegisterRoute(nameof(ServicioDetallePage), typeof(ServicioDetallePage));
        }

        private void ConfigureFlyoutBehavior()
        {
            // Configurar el ancho del flyout para diferentes dispositivos
            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                // En teléfonos, el flyout ocupará el 80% de la pantalla
                FlyoutWidth = DeviceDisplay.MainDisplayInfo.Width * 0.8 / DeviceDisplay.MainDisplayInfo.Density;
            }
            else if (DeviceInfo.Idiom == DeviceIdiom.Tablet)
            {
                // En tablets, un ancho fijo más apropiado
                FlyoutWidth = 300;
            }
            else
            {
                // En desktop, un ancho fijo estándar
                FlyoutWidth = 250;
            }

            // Configurar el comportamiento del flyout
            FlyoutBehavior = FlyoutBehavior.Flyout;
            
            // En dispositivos más grandes, mantener el flyout visible
            if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            {
                FlyoutBehavior = FlyoutBehavior.Locked;
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            
            // Asegurar que el flyout esté cerrado al inicio en dispositivos móviles
            if (DeviceInfo.Idiom == DeviceIdiom.Phone)
            {
                FlyoutIsPresented = false;
            }
        }
    }
}