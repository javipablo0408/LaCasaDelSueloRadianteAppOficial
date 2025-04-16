using LaCasaDelSueloRadianteApp.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.LifecycleEvents;
using System.IO;

namespace LaCasaDelSueloRadianteApp
{
    public static class MauiProgram
    {
        public static MauiApp CreateMauiApp()
        {
            var builder = MauiApp.CreateBuilder();

            // Configuración básica que funciona en todas las plataformas
            ConfigureBasicApp(builder);

            // Configuración específica de plataforma
            ConfigurePlatformSpecific(builder);

            return builder.Build();
        }

        private static void ConfigureBasicApp(MauiAppBuilder builder)
        {
            builder.UseMauiApp<App>();

            // Configurar GraphService
            var graphConfig = new GraphServiceConfiguration
            {
                TenantId = "0cd4c7dd-3fde-4373-8d6f-915d72ab9ce0",
                ClientId = "30af0f82-bbeb-4f49-89cd-3ff526bc339b",
#if WINDOWS
                RedirectUri = "http://localhost",
#else
                RedirectUri = "msal://30af0f82-bbeb-4f49-89cd-3ff526bc339b/",
#endif
                Scopes = new[]
                {
                    "User.Read",
                    "Files.ReadWrite",
                    "Files.ReadWrite.All"
                }
            };

            // Configurar DatabaseService
            var dbPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "clientes.db3");

            // Registrar servicios
            builder.Services.AddSingleton<GraphServiceConfiguration>(graphConfig);
            builder.Services.AddSingleton<GraphService>();
            builder.Services.AddSingleton<DatabaseService>(_ => new DatabaseService(dbPath));

            // Registrar páginas y vistas
            builder.Services.AddTransient<LoginPage>();
            builder.Services.AddTransient<MainPage>();
            builder.Services.AddTransient<HistorialPage>();
            builder.Services.AddTransient<InformacionPage>();
            builder.Services.AddTransient<ClientesPage>();
            builder.Services.AddTransient<AgregarPage>();
            builder.Services.AddSingleton<AppShell>();

            // Registrar servicios de navegación
            builder.Services.AddSingleton<INavigation>(serviceProvider =>
                Application.Current?.MainPage?.Navigation
                ?? throw new InvalidOperationException("Navigation not available"));

            // Configurar logging
#if DEBUG
            builder.Logging.AddDebug();
            builder.Logging.SetMinimumLevel(LogLevel.Trace);
#endif
        }

        private static void ConfigurePlatformSpecific(MauiAppBuilder builder)
        {
#if ANDROID
            if (OperatingSystem.IsAndroidVersionAtLeast(21))
            {
                ConfigureFonts(builder);
                ConfigureAndroid(builder);
            }
#elif IOS || MACCATALYST
            if (OperatingSystem.IsIOSVersionAtLeast(11) || OperatingSystem.IsMacCatalystVersionAtLeast(13, 1))
            {
                ConfigureFonts(builder);
                ConfigureIOS(builder);
            }
#elif WINDOWS
            if (OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763, 0))
            {
                ConfigureFonts(builder);
                ConfigureWindows(builder);
            }
#endif
        }

        private static void ConfigureFonts(MauiAppBuilder builder)
        {
            builder.ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });
        }

#if WINDOWS
        private static void ConfigureWindows(MauiAppBuilder builder)
        {
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddWindows(windowsLifecycleBuilder =>
                {
                    windowsLifecycleBuilder.OnWindowCreated(window =>
                    {
                        window.ExtendsContentIntoTitleBar = false;
                    });
                });
            });
        }
#endif

#if ANDROID
        private static void ConfigureAndroid(MauiAppBuilder builder)
        {
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddAndroid(android =>
                {
                    android.OnCreate((activity, bundle) =>
                    {
                        // Configuración específica de Android
                        Platform.Init(activity, bundle);
                    });
                });
            });
        }
#endif

#if IOS || MACCATALYST
        private static void ConfigureIOS(MauiAppBuilder builder)
        {
            builder.ConfigureLifecycleEvents(events =>
            {
                events.AddiOS(ios =>
                {
                    ios.FinishedLaunching((app, options) =>
                    {
                        // Configuración específica de iOS
                        return true;
                    });
                });
            });
        }
#endif
    }
}