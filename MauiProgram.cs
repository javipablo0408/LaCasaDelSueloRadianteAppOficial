using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Services;
using System.IO;
using Microsoft.Extensions.Logging;

namespace LaCasaDelSueloRadianteApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        AppPaths.EnsureDirectoriesExist();

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
        builder.Services.AddTransient<AgregarPage>();
        builder.Services.AddTransient<ServicioDetallePage>();
        builder.Services.AddTransient<HistorialPage>();
        builder.Services.AddSingleton<AppShell>();

        var app = builder.Build();
        App.Services = app.Services;

        // Sincronización automática de imágenes y total (fire-and-forget)
        var oneDriveService = app.Services.GetRequiredService<OneDriveService>();
        oneDriveService.IniciarSincronizacionAutomaticaImagenes();
        _ = oneDriveService.SincronizarTodoAsync();

        return app;
    }
}