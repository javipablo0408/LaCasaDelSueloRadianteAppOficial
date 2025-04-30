using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;      // MainThread
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp
{
    public partial class App : Application
    {
        /* Contenedor DI accesible globalmente */
        public static IServiceProvider Services { get; set; } = default!;

        public App() // NO lleva parámetros
        {
            InitializeComponent();

            /* Pantalla de carga mientras se inicializa */
            MainPage = new ContentPage
            {
                Content = new ActivityIndicator
                {
                    IsRunning = true,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            _ = InitializeAsync(); // fire-and-forget
        }

        /*-----------------------------------------------------*/
        private async Task InitializeAsync()
        {
            try
            {
                /* 1) Inicializar la base de datos */
                var db = Services.GetRequiredService<DatabaseService>();
                await db.InitAsync();

                /* 2) Comprobar y descargar imágenes */
                await ComprobarYDescargarImagenesAsync(db);

                /* 3) Intento de login silencioso */
                var auth = Services.GetRequiredService<MauiMsalAuthService>();
                var token = await auth.AcquireTokenSilentAsync();

                /* 4) Configurar la página inicial */
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MainPage = token != null
                        ? Services.GetRequiredService<AppShell>() // Usuario autenticado
                        : new NavigationPage(Services.GetRequiredService<LoginPage>()); // Usuario no autenticado
                });
            }
            catch (Exception ex)
            {
                /* Manejo de errores: Mostrar mensaje en pantalla */
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MainPage = new ContentPage
                    {
                        Content = new ScrollView
                        {
                            Content = new Label
                            {
                                Text = $"Error al iniciar la aplicación:\n{ex.Message}",
                                TextColor = Colors.Red,
                                Margin = new Thickness(20),
                                LineBreakMode = LineBreakMode.WordWrap
                            }
                        }
                    };
                });
            }
        }

        /*-----------------------------------------------------*/
        private async Task ComprobarYDescargarImagenesAsync(DatabaseService db)
        {
            try
            {
                var servicios = await db.ObtenerTodosLosServiciosAsync();
                var oneDrive = Services.GetRequiredService<OneDriveService>();

                foreach (var servicio in servicios)
                {
                    // Comprobar y descargar cada imagen si no existe localmente
                    await DescargarImagenSiNoExisteAsync(servicio.FotoPhUrl, "ph", oneDrive);
                    await DescargarImagenSiNoExisteAsync(servicio.FotoConductividadUrl, "conductividad", oneDrive);
                    await DescargarImagenSiNoExisteAsync(servicio.FotoConcentracionUrl, "concentracion", oneDrive);
                    await DescargarImagenSiNoExisteAsync(servicio.FotoTurbidezUrl, "turbidez", oneDrive);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al comprobar o descargar imágenes: {ex.Message}");
            }
        }

        private async Task DescargarImagenSiNoExisteAsync(string? localPath, string tipo, OneDriveService oneDrive)
        {
            if (string.IsNullOrEmpty(localPath) || File.Exists(localPath))
                return;

            try
            {
                var remotePath = $"lacasadelsueloradianteapp/{Path.GetFileName(localPath)}";
                await oneDrive.DescargarImagenSiNoExisteAsync(localPath, remotePath);
                Console.WriteLine($"Imagen {tipo} descargada correctamente: {localPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al descargar la imagen {tipo}: {ex.Message}");
            }
        }
    }
}