using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Services;
using System.IO;
using Microsoft.Extensions.Logging;
using Plugin.LocalNotification;
using Syncfusion.Licensing;
using Microsoft.Maui.Networking;
using LaCasaDelSueloRadianteApp.Pages;

namespace LaCasaDelSueloRadianteApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        SyncfusionLicenseProvider.RegisterLicense("NRAiBiAaIQQuGjN/V09+XU9HdVRFQmJWfFN0QHNedVpxflFGcDwsT3RfQFhjT35Sd0xnW35cdHFTRmteWA==");



        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()
            .ConfigureFonts(f =>
            {
                f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                f.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Servicios
        builder.Services.AddSingleton<MauiMsalAuthService>();
        builder.Services.AddHttpClient<OneDriveService>();
        builder.Services.AddSingleton<IImageService, ImageService>();
        builder.Services.AddSingleton<DatabaseService>(sp =>
        {
            var oneDrive = sp.GetRequiredService<OneDriveService>();
            var logger = sp.GetRequiredService<ILogger<DatabaseService>>();
            return new DatabaseService(oneDrive, logger);
        });

        // Páginas
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<HistorialPage>();
        builder.Services.AddTransient<AgregarPage>();
        builder.Services.AddTransient<ServicioDetallePage>();
        builder.Services.AddSingleton<AppShell>();

        var app = builder.Build();
        App.Services = app.Services;

        // Sincronización automática de imágenes y total (fire-and-forget)
        var oneDriveService = app.Services.GetRequiredService<OneDriveService>();
        System.Diagnostics.Debug.WriteLine("Iniciando sincronización automática de imágenes...");
        oneDriveService.IniciarSincronizacionAutomaticaImagenes();

        System.Diagnostics.Debug.WriteLine("Llamando a SincronizarTodoAsync al iniciar la app...");
        _ = oneDriveService.SincronizarTodoAsync().ContinueWith(t =>
        {
            if (t.Exception != null)
                System.Diagnostics.Debug.WriteLine("Error en SincronizarTodoAsync: " + t.Exception);
            else
                System.Diagnostics.Debug.WriteLine("SincronizarTodoAsync ejecutado correctamente al iniciar la app.");
        });

        // Suscripción a cambios de conectividad para sincronizar cuando vuelva la conexión
        Connectivity.ConnectivityChanged += async (s, e) =>
        {
            if (e.NetworkAccess == NetworkAccess.Internet)
            {
                System.Diagnostics.Debug.WriteLine("Conectividad restaurada. Llamando a SincronizarTodoAsync...");
                var oneDriveService = App.Services.GetRequiredService<OneDriveService>();
                try
                {
                    await oneDriveService.SincronizarTodoAsync();
                    System.Diagnostics.Debug.WriteLine("SincronizarTodoAsync ejecutado tras cambio de conectividad.");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error en SincronizarTodoAsync tras cambio de conectividad: " + ex);
                }
            }
        };

        return app;
    }
}