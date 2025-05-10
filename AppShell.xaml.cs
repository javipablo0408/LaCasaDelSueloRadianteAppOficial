using Microsoft.Maui.Controls;

namespace LaCasaDelSueloRadianteApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Registrar las rutas de las páginas
            Routing.RegisterRoute(nameof(AgregarPage), typeof(AgregarPage));
            Routing.RegisterRoute(nameof(ClientesPage), typeof(ClientesPage));
            Routing.RegisterRoute(nameof(FullScreenImagePage), typeof(FullScreenImagePage));
            Routing.RegisterRoute(nameof(EditarClientePage), typeof(EditarClientePage));
            Routing.RegisterRoute(nameof(HistorialPage), typeof(HistorialPage));
            Routing.RegisterRoute(nameof(InformacionPage), typeof(InformacionPage));
            Routing.RegisterRoute(nameof(LoginPage), typeof(LoginPage));
            Routing.RegisterRoute(nameof(MainPage), typeof(MainPage));
            Routing.RegisterRoute(nameof(ServicioDetallePage), typeof(ServicioDetallePage));
            Routing.RegisterRoute(nameof(ServiciosPage), typeof(ServiciosPage));
        }
    }
}
