using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;
using LaCasaDelSueloRadianteApp.Services;
using Microsoft.Maui.Networking;

namespace LaCasaDelSueloRadianteApp
{
    public partial class App : Application, IDisposable
    {
        public static IServiceProvider Services { get; set; } = default!;

        private readonly System.Timers.Timer _syncTimer;
        private volatile bool _isSyncing = false;
        private readonly object _syncLock = new object();

        private EventHandler<ConnectivityChangedEventArgs> _connectivityChangedHandler;

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
            _syncTimer.Elapsed += OnSyncTimerElapsed;

            _connectivityChangedHandler = HandleConnectivityChanged;
            Connectivity.ConnectivityChanged += _connectivityChangedHandler;

            // Elimina la llamada a MainThread aquí
            // MainThread.BeginInvokeOnMainThread(async () => await InitializeAsync().ConfigureAwait(false));
        }

        protected override void OnStart()
        {
            base.OnStart();
            // Aquí el hilo principal ya está disponible
            MainPage.Dispatcher.DispatchAsync(async () => await InitializeAsync());
        }

        private async void OnSyncTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[SYNC] [TIMER] Temporizador disparado, iniciando sincronización...");
            await VerificarYSincronizarAsync("Temporizador periódico").ConfigureAwait(false);
        }

        private async void HandleConnectivityChanged(object sender, ConnectivityChangedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[SYNC] [CONNECTIVITY] Cambio de conectividad detectado: {e.NetworkAccess}");
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                System.Diagnostics.Debug.WriteLine("[SYNC] [CONNECTIVITY] Conectividad restaurada. Lanzando sincronización...");
                var db = Services.GetService<DatabaseService>();
                if (db != null)
                {
                    await db.IntentarSubidaPendienteBaseDeDatosAsync();
                }
                await VerificarYSincronizarAsync("Conectividad restaurada").ConfigureAwait(false);
            }
        }

        private async Task InitializeAsync()
        {
            System.Diagnostics.Debug.WriteLine("[APP] Iniciando InitializeAsync...");
            try
            {
                var db = Services.GetService<DatabaseService>();
                if (db == null)
                {
                    HandleCriticalError("No se pudo resolver DatabaseService.");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[APP] Inicializando base de datos...");
                await db.InitAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine("[APP] Base de datos inicializada.");

                var auth = Services.GetService<MauiMsalAuthService>();
                if (auth == null)
                {
                    HandleCriticalError("No se pudo resolver MauiMsalAuthService.");
                    return;
                }

                System.Diagnostics.Debug.WriteLine("[MSAL] Intentando obtener token silencioso...");
                var token = await auth.AcquireTokenSilentAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[MSAL] Token silencioso obtenido: {(token != null)}");

                // Elimina el login interactivo automático
                // if (token == null)
                // {
                //     System.Diagnostics.Debug.WriteLine("[MSAL] Intentando login interactivo...");
                //     token = await auth.AcquireTokenInteractiveAsync().ConfigureAwait(false);
                //     System.Diagnostics.Debug.WriteLine($"[MSAL] Login interactivo completado: {(token != null)}");
                // }

                MainPage.Dispatcher.Dispatch(() =>
                {
                    System.Diagnostics.Debug.WriteLine($"[MSAL] Estableciendo MainPage: {(token != null ? "AppShell" : "LoginPage")}");
                    if (token != null)
                    {
                        MainPage = Services.GetRequiredService<AppShell>();
                    }
                    else
                    {
                        MainPage = new NavigationPage(Services.GetRequiredService<LoginPage>());
                    }
                });

                if (token != null)
                {
                    System.Diagnostics.Debug.WriteLine("[SYNC] Autenticación exitosa. Disparando sincronización inicial...");
                    await VerificarYSincronizarAsync("Inicialización tras login").ConfigureAwait(false);

                    if (!_syncTimer.Enabled)
                    {
                        _syncTimer.Start();
                        System.Diagnostics.Debug.WriteLine("[SYNC] Temporizador de sincronización periódica iniciado.");
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SYNC] Login no exitoso. No se inicia sincronización ni temporizador.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Falla crítica en InitializeAsync: {ex}");
                HandleCriticalError($"Error al inicializar la aplicación: {ex.Message}");
            }
            System.Diagnostics.Debug.WriteLine("[APP] InitializeAsync completado.");
        }

        private void HandleCriticalError(string errorMessage)
        {
            System.Diagnostics.Debug.WriteLine($"[CRITICAL ERROR HANDLER] Mensaje: {errorMessage}");
            try
            {
                if (MainThread.IsMainThread)
                {
                    SetErrorPage(errorMessage);
                }
                else
                {
                    try
                    {
                        MainPage.Dispatcher.Dispatch(() => SetErrorPage(errorMessage));
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[CRITICAL ERROR HANDLER] No se pudo invocar en el hilo principal: {ex.Message}");
                        SetErrorPage(errorMessage);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CRITICAL ERROR HANDLER] Falló al mostrar la página de error: {ex.Message}");
            }
        }

        private void SetErrorPage(string errorMessage)
        {
            MainPage = new ContentPage
            {
                Content = new ScrollView
                {
                    Padding = new Thickness(20),
                    Content = new VerticalStackLayout
                    {
                        Spacing = 10,
                        VerticalOptions = LayoutOptions.Center,
                        HorizontalOptions = LayoutOptions.Center,
                        Children =
                        {
                            new Label
                            {
                                Text = "Error Crítico",
                                FontSize = 24,
                                HorizontalTextAlignment = TextAlignment.Center,
                                TextColor = Colors.Red
                            },
                            new Label
                            {
                                Text = errorMessage + "\n\nPor favor, cierra y reinicia la aplicación. Si el problema persiste, contacta a soporte.",
                                FontSize = 16,
                                HorizontalTextAlignment = TextAlignment.Center
                            }
                        }
                    }
                }
            };
        }

        private async Task VerificarYSincronizarAsync(string triggerSource)
        {
            bool acquiredLock = false;
            lock (_syncLock)
            {
                if (!_isSyncing)
                {
                    _isSyncing = true;
                    acquiredLock = true;
                }
            }

            if (!acquiredLock)
            {
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Sincronización ya en progreso. Omitiendo esta llamada.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Iniciando ciclo de sincronización completo...");

            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) No hay conexión a Internet. Sincronización omitida.");
                    return;
                }

                var db = Services.GetService<DatabaseService>();
                var oneDrive = Services.GetService<OneDriveService>();

                if (db == null || oneDrive == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Error: No se pudo resolver DatabaseService o OneDriveService.");
                    throw new InvalidOperationException("Dependencias de servicio no resueltas para la sincronización.");
                }

                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 1: Subiendo/Fusionando SyncQueue (syncqueue.json)...");
                await db.SubirSyncQueueAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 1 completado.");

                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 2: Descargando y aplicando SyncQueue de OneDrive...");
                await db.SincronizarDispositivoNuevoAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 2 completado.");

                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 3: Sincronizando registros (IsSynced / sync_*.json)...");
                await oneDrive.SincronizarRegistrosAsync(db).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 3 completado.");

                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 4: Sincronizando imágenes...");
                await oneDrive.SincronizarImagenesAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 4 completado.");

                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Ciclo de sincronización completo finalizado exitosamente.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) [ERROR] Durante la sincronización: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                lock (_syncLock)
                {
                    _isSyncing = false;
                }
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Flag _isSyncing reseteado a false.");
            }
        }

        public async Task IntentarSubidaInmediataDeCambiosAsync(string motivo = "Cambio local inmediato")
        {
            bool acquiredLock = false;
            lock (_syncLock)
            {
                if (!_isSyncing)
                {
                    _isSyncing = true;
                    acquiredLock = true;
                }
            }

            if (!acquiredLock)
            {
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) Sincronización ya en progreso. Subida inmediata omitida.");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) Iniciando intento de subida inmediata de cambios...");

            try
            {
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) Sin conexión a Internet, subida inmediata omitida.");
                    return;
                }

                var db = Services.GetService<DatabaseService>();
                var oneDrive = Services.GetService<OneDriveService>();

                if (db == null || oneDrive == null)
                {
                    System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) Error: No se pudo resolver DatabaseService o OneDriveService.");
                    throw new InvalidOperationException("Dependencias de servicio no resueltas para la subida inmediata.");
                }

                System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) Subiendo/Fusionando SyncQueue (syncqueue.json)...");
                await db.SubirSyncQueueAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) SyncQueue subida/fusionada.");

                System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) Subiendo registros locales (IsSynced / sync_*.json)...");
                await oneDrive.SubirCambiosLocalesAsync(db).ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) Registros locales subidos.");

                System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) Intento de subida inmediata de cambios finalizado.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) [ERROR] Falló la subida inmediata de cambios: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                lock (_syncLock)
                {
                    _isSyncing = false;
                }
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) Flag _isSyncing reseteado a false.");
            }
        }

        protected override void OnSleep()
        {
            _syncTimer?.Stop();
            System.Diagnostics.Debug.WriteLine("[APP LIFECYCLE] OnSleep: Temporizador de sincronización detenido.");
        }

        protected override void OnResume()
        {
            if (_syncTimer != null && MainPage is AppShell)
            {
                _syncTimer.Start();
                System.Diagnostics.Debug.WriteLine("[APP LIFECYCLE] OnResume: Temporizador de sincronización iniciado/reanudado.");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine("[APP LIFECYCLE] OnResume: Temporizador no iniciado (MainPage no es AppShell o _syncTimer es null).");
            }
        }

        private bool _disposed = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _syncTimer?.Stop();
                    _syncTimer?.Dispose();

                    if (_connectivityChangedHandler != null)
                    {
                        Connectivity.ConnectivityChanged -= _connectivityChangedHandler;
                        _connectivityChangedHandler = null;
                    }
                    System.Diagnostics.Debug.WriteLine("[APP LIFECYCLE] Dispose: Recursos limpiados.");
                }
                _disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
