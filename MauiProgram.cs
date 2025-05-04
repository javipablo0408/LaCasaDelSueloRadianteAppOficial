using CommunityToolkit.Maui;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Storage;
using LaCasaDelSueloRadianteApp.Services;

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

        /* ---------- Servicios ---------- */
        builder.Services.AddSingleton<MauiMsalAuthService>();
        builder.Services.AddSingleton<OneDriveService>();
        builder.Services.AddSingleton<IImageService, ImageService>();   // NUEVO
        builder.Services.AddSingleton<DatabaseService>(sp =>
        {
            var dbFile = Path.Combine(FileSystem.AppDataDirectory, "clientes.db3");
            return new DatabaseService(dbFile, sp.GetRequiredService<OneDriveService>());
        });

        /* ---------- Páginas ---------- */
        builder.Services.AddTransient<LoginPage>();
        builder.Services.AddTransient<AgregarPage>();
        builder.Services.AddTransient<ServicioDetallePage>();
        builder.Services.AddTransient<HistorialPage>();  // Añadido HistorialPage
        builder.Services.AddSingleton<AppShell>();

        var app = builder.Build();
        App.Services = app.Services;            // acceso global opcional
        return app;
    }
}