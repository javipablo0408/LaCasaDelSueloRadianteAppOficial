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
                Content = new VerticalStackLayout
                {
                    Spacing = 20,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center,
                    Children =
                    {
                        new ActivityIndicator
                        {
                            IsRunning = true,
                            HorizontalOptions = LayoutOptions.Center,
                            Color = Colors.Blue
                        },
                        new Label
                        {
                            Text = "Un momento...",
                            FontSize = 18,
                            FontAttributes = FontAttributes.Bold,
                            HorizontalTextAlignment = TextAlignment.Center,
                            TextColor = Colors.DarkBlue
                        },
                        new Label
                        {
                            Text = "Comprobando cambios y sincronizando datos",
                            FontSize = 14,
                            HorizontalTextAlignment = TextAlignment.Center,
                            TextColor = Colors.Gray
                        }
                    }
                }
            };

            _syncTimer = new System.Timers.Timer(TimeSpan.FromMinutes(2).TotalMilliseconds) // Cada 2 minutos para sincronización completa
            {
                AutoReset = true,
                Enabled = false
            };
            _syncTimer.Elapsed += OnSyncTimerElapsed;

            _connectivityChangedHandler = HandleConnectivityChanged;
            Connectivity.ConnectivityChanged += _connectivityChangedHandler;
        }

        protected override void OnStart()
        {
            base.OnStart();
            // Aquí el hilo principal ya está disponible
            MainPage.Dispatcher.DispatchAsync(async () => await InitializeAsync());
        }

        private async void OnSyncTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine("[SYNC] [TIMER] Temporizador disparado, verificando si hay cambios o se necesita sincronización...");
            
            // Verificar si hay cambios pendientes O si ha pasado suficiente tiempo desde la última sincronización completa
            var db = Services.GetService<DatabaseService>();
            bool deberiaEjecutarSync = true; // Por defecto, ejecutar sincronización
            
            if (db != null)
            {
                try
                {
                    var hayCambiosPendientes = await db.HayCambiosPendientesAsync();
                    if (!hayCambiosPendientes)
                    {
                        // Aún sin cambios locales, ejecutar sincronización cada cierto tiempo para verificar cambios remotos
                        System.Diagnostics.Debug.WriteLine("[SYNC] [TIMER] No hay cambios locales pendientes, pero ejecutando sincronización para verificar cambios remotos.");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[SYNC] [TIMER] Hay cambios pendientes, ejecutando sincronización completa.");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SYNC] [TIMER] Error verificando cambios: {ex.Message}. Ejecutando sincronización por seguridad.");
                }
            }
            
            if (deberiaEjecutarSync)
            {
                await VerificarYSincronizarAsync("Temporizador periódico").ConfigureAwait(false);
            }
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

                System.Diagnostics.Debug.WriteLine("[APP] Intentando autenticación silenciosa...");
                var token = await auth.AcquireTokenSilentAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[APP] ¿Autenticación exitosa? {token != null}");

                if (token != null)
                {
                    // Actualizar mensaje de pantalla de carga
                    MainPage.Dispatcher.Dispatch(() =>
                    {
                        if (MainPage is ContentPage contentPage && contentPage.Content is VerticalStackLayout layout && layout.Children.Count >= 3)
                        {
                            if (layout.Children[2] is Label statusLabel)
                            {
                                statusLabel.Text = "Sincronizando cambios con otros dispositivos...";
                            }
                        }
                    });

                    System.Diagnostics.Debug.WriteLine("[SYNC] Autenticación exitosa. Iniciando sincronización completa ANTES de mostrar la interfaz...");
                    
                    try
                    {
                        // Realizar sincronización completa antes de mostrar la interfaz
                        await VerificarYSincronizarAsync("Sincronización inicial al abrir app").ConfigureAwait(false);
                        System.Diagnostics.Debug.WriteLine("[SYNC] Sincronización inicial completada exitosamente.");
                    }
                    catch (Exception syncEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[SYNC] Error en sincronización inicial: {syncEx.Message}");
                        // Continuar aunque falle la sincronización inicial
                    }

                    // Ahora mostrar la interfaz principal
                    MainPage.Dispatcher.Dispatch(() =>
                    {
                        System.Diagnostics.Debug.WriteLine("[APP] Estableciendo MainPage: AppShell (después de sincronización)");
                        MainPage = Services.GetRequiredService<AppShell>();
                    });
                }
                else
                {
                    // Sin autenticación, mostrar login
                    MainPage.Dispatcher.Dispatch(() =>
                    {
                        System.Diagnostics.Debug.WriteLine("[APP] Estableciendo MainPage: LoginPage");
                        MainPage = new NavigationPage(Services.GetRequiredService<LoginPage>());
                    });
                }

                if (token != null)
                {
                    System.Diagnostics.Debug.WriteLine("[SYNC] Autenticación exitosa. Iniciando sincronización inicial en background...");
                    
                    // Iniciar sincronización en background sin bloquear la UI
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(2000); // Esperar 2 segundos para que la UI se estabilice
                            await VerificarYSincronizarAsync("Inicialización tras login").ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SYNC] Error en sincronización inicial: {ex.Message}");
                        }
                    });

                    if (!_syncTimer.Enabled)
                    {
                        _syncTimer.Start();
                        System.Diagnostics.Debug.WriteLine("[SYNC] Temporizador de sincronización periódica iniciado.");
                    }

                    // Iniciar servicios adicionales en background
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await Task.Delay(1000); // Esperar 1 segundo
                            var oneDrive = Services.GetService<OneDriveService>();
                            if (oneDrive != null)
                            {
                                oneDrive.IniciarSincronizacionAutomaticaImagenes();
                                System.Diagnostics.Debug.WriteLine("[SYNC] Sincronización automática de imágenes iniciada (cada 10 minutos).");
                            }
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[SYNC] Error iniciando servicios adicionales: {ex.Message}");
                        }
                    });
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("[SYNC] Login no exitoso. No se inicia sincronización ni temporizador.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ERROR] Falla crítica en InitializeAsync: {ex}");
                System.Diagnostics.Debug.WriteLine($"[ERROR] StackTrace: {ex.StackTrace}");
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
                    System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Error: No se pudieron resolver los servicios de base de datos o OneDrive.");
                    throw new InvalidOperationException("Servicios de sincronización no resueltos.");
                }

                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Ejecutando sincronización bilateral completa...");
                
                // 1. Bajar cambios desde OneDrive
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 1: Bajando cambios desde OneDrive...");
                await oneDrive.DescargarYFusionarCambiosDeTodosLosDispositivosAsync(db).ConfigureAwait(false);
                
                // 2. Subir cambios locales
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 2: Subiendo cambios locales...");
                await db.SubirSyncQueueAsync().ConfigureAwait(false);
                await oneDrive.SubirCambiosLocalesAsync(db).ConfigureAwait(false);
                
                // 3. Sincronizar imágenes
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 3: Sincronizando imágenes...");
                await oneDrive.SincronizarImagenesAsync().ConfigureAwait(false);
                
                // 4. Subir versión final de base de datos
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 4: Subiendo versión final de base de datos...");
                await db.SubirUltimaVersionBaseDeDatosAsync().ConfigureAwait(false);
                
                // 5. Limpieza de archivos antiguos
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Paso 5: Realizando limpieza...");
                await oneDrive.LimpiarArchivosSyncAntiguosAsync(30).ConfigureAwait(false);
                
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({triggerSource}) Sincronización bilateral completa finalizada exitosamente.");
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

                System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) Subiendo base de datos actualizada...");
                await db.SubirUltimaVersionBaseDeDatosAsync().ConfigureAwait(false);
                System.Diagnostics.Debug.WriteLine($"[SYNC] ({motivo}) Base de datos subida.");

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
            
            // Ejecutar sincronización inmediata cuando la app vuelve del background
            System.Diagnostics.Debug.WriteLine("[APP] Aplicación resumed, ejecutando sincronización para obtener cambios remotos...");
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(1000); // Esperar un segundo para que la app se estabilice
                    await VerificarYSincronizarAsync("App resumed").ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[APP] Error en sincronización al resumir: {ex.Message}");
                }
            });
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

                    // Detener sincronización automática de imágenes
                    var oneDrive = Services?.GetService<OneDriveService>();
                    if (oneDrive != null)
                    {
                        oneDrive.DetenerSincronizacionAutomaticaImagenes();
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
