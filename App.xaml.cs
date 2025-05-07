using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using LaCasaDelSueloRadianteApp.Services;
using Microsoft.Maui.Storage;

namespace LaCasaDelSueloRadianteApp
{
    public partial class App : Application, IDisposable
    {
        public static IServiceProvider Services { get; set; } = default!;

        private readonly System.Timers.Timer _syncTimer;

        public App()
        {
            InitializeComponent();

            // Página de carga inicial
            MainPage = new ContentPage
            {
                Content = new ActivityIndicator
                {
                    IsRunning = true,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            // Configurar el temporizador para sincronización periódica (cada 15 minutos)
            _syncTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds)
            {
                AutoReset = true,
                Enabled = false
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
                System.Diagnostics.Debug.WriteLine("Base de datos inicializada correctamente.");

                // Comprueba imágenes y la base de datos local (se restaura si es necesario)
                await ComprobarYDescargarImagenesYBaseDeDatosAsync(db);
                System.Diagnostics.Debug.WriteLine("Verificación de imágenes y base de datos completada.");

                var auth = Services.GetRequiredService<MauiMsalAuthService>();
                var token = await auth.AcquireTokenSilentAsync();
                System.Diagnostics.Debug.WriteLine("Autenticación completada.");

                // Establecer la MainPage según la autenticación
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MainPage = token != null
                        ? Services.GetRequiredService<AppShell>()
                        : new NavigationPage(Services.GetRequiredService<LoginPage>());
                });

                // Inicia la sincronización periódica
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
                await ComprobarYDescargarImagenesYBaseDeDatosAsync(db);

                var oneDrive = Services.GetRequiredService<OneDriveService>();
                // Ejecutar la sincronización bidireccional de forma periódica.
                await oneDrive.SincronizarBidireccionalAsync();
                System.Diagnostics.Debug.WriteLine("Sincronización periódica completada.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error durante la sincronización periódica: {ex.Message}");
            }
        }

        /// <summary>
        /// Comprueba y descarga imágenes y la base de datos local.
        /// Si el archivo de la base de datos no está, lo restaura desde OneDrive.
        /// </summary>
        private async Task ComprobarYDescargarImagenesYBaseDeDatosAsync(DatabaseService db)
        {
            try
            {
                var servicios = await db.ObtenerTodosLosServiciosAsync();
                var oneDrive = Services.GetRequiredService<OneDriveService>();

                // Comprobar imágenes asociadas a cada servicio.
                foreach (var servicio in servicios)
                {
                    await DescargarImagenSiNoExisteAsync(servicio.FotoPhUrl, "ph", oneDrive);
                    await DescargarImagenSiNoExisteAsync(servicio.FotoConductividadUrl, "conductividad", oneDrive);
                    await DescargarImagenSiNoExisteAsync(servicio.FotoConcentracionUrl, "concentracion", oneDrive);
                    await DescargarImagenSiNoExisteAsync(servicio.FotoTurbidezUrl, "turbidez", oneDrive);
                }

                // Verificar que la base de datos exista localmente.
                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "clientes.db3");
                if (!File.Exists(dbPath))
                {
                    await oneDrive.RestaurarBaseDeDatosAsync(dbPath);
                    System.Diagnostics.Debug.WriteLine("Base de datos restaurada desde OneDrive.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error al comprobar imágenes y base de datos: {ex.Message}");
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

        public void Dispose()
        {
            _syncTimer?.Dispose();
        }
    }
}