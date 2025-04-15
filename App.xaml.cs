namespace LaCasaDelSueloRadianteApp
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();

            Preferences.Clear();//BORRALO LUEGO 

            // Verificar si el usuario está logueado
            bool isLoggedIn = Preferences.Get("IsLoggedIn", false);

            if (isLoggedIn)
            {
                MainPage = new AppShell(); // Navegación con barra de pestañas
            }
            else
            {
                MainPage = new NavigationPage(new LoginPage()); // Página de login
            }
        }
    }
}
