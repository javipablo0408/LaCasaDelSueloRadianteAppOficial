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
        private bool _isSyncing = false;

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

            _syncTimer = new System.Timers.Timer(TimeSpan.FromMinutes(1).TotalMilliseconds)
            {
                AutoReset = true,
                Enabled = false
            };
            _syncTimer.Elapsed += async (s, e) => await VerificarYSincronizarAsync();

            // Ejecutamos la inicialización fuera del constructor para evitar bucles tras el login interactivo
            MainThread.BeginInvokeOnMainThread(async () => await InitializeAsync());
        }

        private async Task InitializeAsync()
        {
            try
            {
                var db = Services.GetService<DatabaseService>();
                if (db == null)
                    throw new InvalidOperationException("No se pudo resolver DatabaseService.");

                await db.InitAsync();
                await ComprobarYDescargarImagenesYBaseDeDatosAsync(db);

                var auth = Services.GetService<MauiMsalAuthService>();
                if (auth == null)
                    throw new InvalidOperationException("No se pudo resolver MauiMsalAuthService.");

                var token = await auth.AcquireTokenSilentAsync();
                System.Diagnostics.Debug.WriteLine("[MSAL] Token silencioso: " + (token != null));

                if (token == null)
                {
                    System.Diagnostics.Debug.WriteLine("[MSAL] Intentando login interactivo...");
                    token = await auth.AcquireTokenInteractiveAsync();
                    System.Diagnostics.Debug.WriteLine("[MSAL] Login interactivo completado: " + (token != null));
                }

                MainThread.BeginInvokeOnMainThread(() =>
                {
                    System.Diagnostics.Debug.WriteLine("[MSAL] Estableciendo MainPage: " + (token != null ? "AppShell" : "LoginPage"));
                    MainPage = token != null
                        ? Services.GetRequiredService<AppShell>()
                        : new NavigationPage(Services.GetRequiredService<LoginPage>());
                });

                _syncTimer.Start();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] InitializeAsync: {ex}");

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
            if (_isSyncing) return;
            _isSyncing = true;
            try
            {
                var db = Services.GetService<DatabaseService>();
                var oneDrive = Services.GetService<OneDriveService>();
                if (db == null || oneDrive == null)
                    throw new InvalidOperationException("No se pudo resolver DatabaseService o OneDriveService.");

                await ComprobarYDescargarImagenesYBaseDeDatosAsync(db);
                await oneDrive.SincronizarRegistrosAsync(db);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Sincronización periódica: {ex.Message}");
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private async Task ComprobarYDescargarImagenesYBaseDeDatosAsync(DatabaseService db)
        {
            try
            {
                var servicios = await db.ObtenerServiciosAsync(0);
                var oneDrive = Services.GetService<OneDriveService>();
                if (oneDrive == null)
                    throw new InvalidOperationException("No se pudo resolver OneDriveService.");

                foreach (var servicio in servicios)
                {
                    await DescargarImagenSiNoExisteAsync(servicio.FotoPhUrl, "ph", oneDrive);
                    await DescargarImagenSiNoExisteAsync(servicio.FotoConductividadUrl, "conductividad", oneDrive);
                    await DescargarImagenSiNoExisteAsync(servicio.FotoConcentracionUrl, "concentracion", oneDrive);
                    await DescargarImagenSiNoExisteAsync(servicio.FotoTurbidezUrl, "turbidez", oneDrive);
                }

                var dbPath = Path.Combine(FileSystem.AppDataDirectory, "clientes.db3");
                if (!File.Exists(dbPath))
                {
                    await oneDrive.RestaurarBaseDeDatosAsync(dbPath);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Comprobar imágenes y DB: {ex.Message}");
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
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Descargar imagen {tipo}: {ex.Message}");
            }
        }

        protected override void OnSleep()
        {
            _syncTimer?.Stop();
        }

        protected override void OnResume()
        {
            _syncTimer?.Start();
        }

        public void Dispose()
        {
            _syncTimer?.Dispose();
        }
    }
}
