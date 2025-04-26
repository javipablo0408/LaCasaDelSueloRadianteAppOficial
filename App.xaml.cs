using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Microsoft.Maui.Storage;

namespace LaCasaDelSueloRadianteApp
{
    public partial class App : Application
    {
        private readonly IServiceProvider _serviceProvider;

        /*------------------------------------------------------------
         *  Propiedad estática: da acceso global al contenedor DI
         *-----------------------------------------------------------*/
        public static IServiceProvider Services { get; private set; } = default!;

        public App(IServiceProvider serviceProvider)
        {
            /*----------------  Manejo de excepciones globales  ----------------*/
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
                Debug.WriteLine("[Unhandled] " + (e.ExceptionObject as Exception));
            TaskScheduler.UnobservedTaskException += (_, e) =>
            {
                Debug.WriteLine("[Unobserved] " + e.Exception);
                e.SetObserved();
            };
#if WINDOWS
            Microsoft.UI.Xaml.Application.Current.UnhandledException += (_, e) =>
            {
                Debug.WriteLine("[WinUI] " + e.Exception);
                e.Handled = true;
            };
#endif
            /*----------------  Inyección de dependencias  ----------------*/
            _serviceProvider = serviceProvider;
            Services = serviceProvider;          //  ← expone el contenedor

            InitializeComponent();

            /*----------------  Selección de página inicial  ----------------*/
            try
            {
                //  ⚠️  Mientras depuras, puedes forzar 'false' aquí.
                bool estaLogueado = Preferences.Default.Get("IsLoggedIn", false);

                if (estaLogueado)
                {
                    // Usuario autenticado → AppShell con pestañas
                    MainPage = _serviceProvider.GetRequiredService<AppShell>();
                }
                else
                {
                    // No autenticado → LoginPage dentro de NavigationPage
                    var login = _serviceProvider.GetRequiredService<LoginPage>();
                    MainPage = new NavigationPage(login);
                }
            }
            catch (Exception ex)
            {
                // Si algo explota en el arranque, lo mostramos en pantalla
                MainPage = new ContentPage
                {
                    Content = new ScrollView
                    {
                        Content = new Label
                        {
                            Text = $"Se ha producido un error al iniciar la app:\n{ex}",
                            TextColor = Colors.Red,
                            LineBreakMode = LineBreakMode.WordWrap,
                            Margin = new Thickness(20)
                        }
                    }
                };
            }
        }
    }
}