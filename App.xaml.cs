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

        public App(IServiceProvider serviceProvider)
        {
            // 1) Suscribir excepciones globales para capturar errores no gestionados
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Debug.WriteLine("Excepción no controlada: " +
                    (e.ExceptionObject as Exception)?.ToString());
            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Debug.WriteLine("Excepción no observada en Task: " +
                    e.Exception.ToString());
                e.SetObserved();
            };

            _serviceProvider = serviceProvider;
            InitializeComponent();

        

            try
            {
                // 2) Verificar si el usuario está logueado
                bool estaLogueado = Preferences.Default.Get("IsLoggedIn", false);

                if (estaLogueado)
                {
                    // Ya autenticado → AppShell como MainPage
                    MainPage = _serviceProvider.GetRequiredService<AppShell>();
                }
                else
                {
                    // No autenticado → pantalla de login dentro de NavigationPage
                    var loginPage = _serviceProvider.GetRequiredService<LoginPage>();
                    MainPage = new NavigationPage(loginPage);
                }
            }
            catch (Exception ex)
            {
                // 3) Mostrar la excepción en pantalla en lugar de cerrar la app
                MainPage = new ContentPage
                {
                    Content = new ScrollView
                    {
                        Content = new Label
                        {
                            Text = $"Se ha producido un error:\n{ex}",
                            TextColor = Colors.Red,
                            LineBreakMode = LineBreakMode.WordWrap
                        }
                    }
                };
            }
        }
    }
}