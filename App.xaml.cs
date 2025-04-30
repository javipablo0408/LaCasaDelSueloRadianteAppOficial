using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Dispatching;      // MainThread
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp
{
    public partial class App : Application
    {
        /* Contenedor DI accesible globalmente */
        public static IServiceProvider Services { get; set; } = default!;

        public App() // NO lleva parámetros
        {
            InitializeComponent();

            /* Pantalla de carga mientras se inicializa */
            MainPage = new ContentPage
            {
                Content = new ActivityIndicator
                {
                    IsRunning = true,
                    HorizontalOptions = LayoutOptions.Center,
                    VerticalOptions = LayoutOptions.Center
                }
            };

            _ = InitializeAsync(); // fire-and-forget
        }

        /*-----------------------------------------------------*/
        private async Task InitializeAsync()
        {
            try
            {
                /* 1) Init BD (crea tablas si no existen o restaura desde OneDrive) */
                var db = Services.GetRequiredService<DatabaseService>();
                await db.InitAsync();

                /* 2) Intento de login silencioso */
                var auth = Services.GetRequiredService<MauiMsalAuthService>();
                var token = await auth.AcquireTokenSilentAsync();

                /* 3) Decidir página inicial en el hilo UI */
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MainPage = token != null
                        ? Services.GetRequiredService<AppShell>()
                        : new NavigationPage(Services.GetRequiredService<LoginPage>());
                });
            }
            catch (Exception ex)
            {
                /* Si algo falla, mostrar el mensaje */
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    MainPage = new ContentPage
                    {
                        Content = new ScrollView
                        {
                            Content = new Label
                            {
                                Text = $"Error al iniciar:\n{ex}",
                                TextColor = Colors.Red,
                                Margin = new Thickness(20),
                                LineBreakMode = LineBreakMode.WordWrap
                            }
                        }
                    };
                });
            }
        }
    }
}