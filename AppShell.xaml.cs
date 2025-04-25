using Microsoft.Maui.Controls;

namespace LaCasaDelSueloRadianteApp
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();

            // Registrar la ruta para AgregarPage
            Routing.RegisterRoute(nameof(AgregarPage), typeof(AgregarPage));
        }
    }
}