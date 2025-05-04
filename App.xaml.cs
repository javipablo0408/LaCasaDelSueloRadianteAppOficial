using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching; // Para MainThread
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp
{
    public partial class App : Application
    {
        /* Contenedor DI accesible globalmente */
        public static IServiceProvider Services { get; set; } = default!;

        private readonly System.Timers.Timer _syncTimer; // Especificar el espacio de nombres completo

        public App()
        {
            InitializeComponent();

            MainPage = new ContentPage
            {
                Content = new ActivityIndicator
                {
                    IsRunning = true,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            // Configurar el temporizador estándar
            _syncTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds)
            {
                AutoReset = true, // Repetir automáticamente
                Enabled = false   // Iniciar manualmente después de la inicialización
            };
            _syncTimer.Elapsed += async (s, e) => await VerificarYSincronizarAsync();

            _ = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            try
            {
                var db = Services.GetRequiredService<DatabaseService>();
                await db.InitAsync();

                await ComprobarYDescargarImagenesAsync(db);

                var auth = Services.GetRequiredService<MauiMsalAuthService>();
                var token = await auth.AcquireTokenSilentAsync();

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MainPage = token != null
                        ? Services.GetRequiredService<AppShell>()
                        : new NavigationPage(Services.GetRequiredService<LoginPage>());
                });

                // Iniciar el temporizador después de la inicialización
                _syncTimer.Start();
            }
            catch (Exception ex)
            {
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

        private async Task VerificarYSincronizarAsync()
        {
            try
            {
                var db = Services.GetRequiredService<DatabaseService>();
                var oneDrive = Services.GetRequiredService<OneDriveService>();

                await ComprobarYDescargarImagenesAsync(db);
                await SincronizarDatosLocalesAsync(db, oneDrive);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error durante la sincronización periódica: {ex.Message}");
            }
        }

        private async Task ComprobarYDescargarImagenesAsync(DatabaseService db)
        {
            try
            {
                var servicios = await db.ObtenerTodosLosServiciosAsync();
                var oneDrive = Services.GetRequiredService<OneDriveService>();

                foreach (var servicio in servicios)
                {
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

        private async Task SincronizarDatosLocalesAsync(DatabaseService db, OneDriveService oneDrive)
        {
            try
            {
                var cambiosLocales = oneDrive.DetectarCambiosLocales();

                foreach (var cambio in cambiosLocales)
                {
                    if (cambio.Tipo == "Archivo")
                    {
                        using var stream = File.OpenRead(cambio.RutaLocal);
                        await oneDrive.UploadFileAsync(cambio.RutaRemota, stream);
                        Console.WriteLine($"Archivo sincronizado: {cambio.RutaLocal}");
                    }
                    else if (cambio.Tipo == "BaseDeDatos")
                    {
                        await oneDrive.BackupBaseDeDatosAsync();
                        Console.WriteLine("Base de datos sincronizada con OneDrive.");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al sincronizar datos locales: {ex.Message}");
            }
        }
    }
}