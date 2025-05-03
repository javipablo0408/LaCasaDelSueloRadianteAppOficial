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

            /* Configurar el contenedor de dependencias */
            ConfigureServices();

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
                System.Diagnostics.Debug.WriteLine("Iniciando la aplicación...");

                /* 1) Inicializar la base de datos */
                var db = Services.GetRequiredService<DatabaseService>();
                await db.InitAsync();
                System.Diagnostics.Debug.WriteLine("Base de datos inicializada correctamente.");

                /* 2) Comprobar y descargar imágenes */
                await ComprobarYDescargarImagenesAsync(db);
                System.Diagnostics.Debug.WriteLine("Imágenes comprobadas correctamente.");

                /* 3) Intento de login silencioso */
                var auth = Services.GetRequiredService<MauiMsalAuthService>();
                var token = await auth.AcquireTokenSilentAsync();
                System.Diagnostics.Debug.WriteLine("Autenticación completada.");

                /* 4) Configurar la página inicial */
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MainPage = token != null
                        ? Services.GetRequiredService<AppShell>() // Usuario autenticado
                        : new NavigationPage(Services.GetRequiredService<LoginPage>()); // Usuario no autenticado

                    System.Diagnostics.Debug.WriteLine("Página principal configurada.");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al iniciar la aplicación: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}");

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
                System.Diagnostics.Debug.WriteLine($"Error al comprobar o descargar imágenes: {ex.Message}");
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
                System.Diagnostics.Debug.WriteLine($"Imagen {tipo} descargada correctamente: {localPath}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al descargar la imagen {tipo}: {ex.Message}");
            }
        }

        /*-----------------------------------------------------*/
        private void ConfigureServices()
        {
            var services = new ServiceCollection();

            /* Registrar servicios necesarios */
            services.AddSingleton<DatabaseService>(provider =>
            {
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "clientes.db3");
                var oneDrive = provider.GetRequiredService<OneDriveService>();
                return new DatabaseService(dbPath, oneDrive);
            });

            services.AddSingleton<OneDriveService>();
            services.AddSingleton<MauiMsalAuthService>();
            services.AddSingleton<IImageService, ImageService>();

            /* Registrar páginas */
            services.AddSingleton<AppShell>();
            services.AddTransient<LoginPage>();
            services.AddTransient<ClientesPage>();
            services.AddTransient<ServiciosPage>();
            services.AddTransient<EditarClientePage>();
            services.AddTransient<HistorialPage>(); // Añadido HistorialPage

            /* Configurar el contenedor de servicios */
            Services = services.BuildServiceProvider();
        }
    }
}