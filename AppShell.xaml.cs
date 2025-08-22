using Microsoft.Maui.Controls;

namespace LaCasaDelSueloRadianteApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            Routing.RegisterRoute(nameof(AgregarPage), typeof(AgregarPage));
            Routing.RegisterRoute(nameof(FullScreenImagePage), typeof(FullScreenImagePage));
            Routing.RegisterRoute(nameof(ServicioDetallePage), typeof(ServicioDetallePage));
        }
    }
}