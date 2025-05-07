using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Services;
using System.IO;

namespace LaCasaDelSueloRadianteApp;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .UseMauiCommunityToolkit()          // Toolkit
            .ConfigureFonts(f =>
            {
                f.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                f.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Registrar servicios
        builder.Services.AddSingleton<MauiMsalAuthService>();
        builder.Services.AddSingleton<OneDriveService>();
        builder.Services.AddHostedService<OneDriveSyncService>(); // Servicio en segundo plano
        // Otros servicios, ejemplo para imágenes y base de datos:
        builder.Services.AddSingleton<IImageService, ImageService>();
        builder.Services.AddSingleton<DatabaseService>(sp =>
        {
            var dbFile = Path.Combine(FileSystem.AppDataDirectory, "clientes.db3");
            return new DatabaseService(dbFile, sp.GetRequiredService<OneDriveService>());
        });

        // Registrar páginas
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<AgregarPage>();
        builder.Services.AddTransient<ServicioDetallePage>();
        builder.Services.AddTransient<HistorialPage>();
        builder.Services.AddSingleton<AppShell>();

        var app = builder.Build();
        App.Services = app.Services; // Acceso global opcional
        return app;
    }
}