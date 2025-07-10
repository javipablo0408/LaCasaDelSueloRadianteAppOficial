using LaCasaDelSueloRadianteApp.Models;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp;

public partial class InformacionPage : ContentPage
{
    private readonly DatabaseService _db;

    public InformacionPage()
    {
        InitializeComponent();
        _db = App.Services.GetRequiredService<DatabaseService>();
        CargarDatos();
    }

    private async void OnGuardarClicked(object sender, EventArgs e)
    {
        var instalador = new Instalador
        {
            Nombre = NombreEntry.Text ?? "",
            Empresa = EmpresaEntry.Text ?? "",
            CifNif = CifNifEntry.Text ?? "",
            Direccion = DireccionEntry.Text ?? "",
            Telefono = TelefonoEntry.Text ?? "",
            Mail = MailEntry.Text ?? ""
        };

        await _db.GuardarInstaladorAsync(instalador);
        await DisplayAlert("Guardado", "Datos del instalador guardados correctamente.", "OK");
    }

    private async void CargarDatos()
    {
        var instalador = await _db.ObtenerInstaladorAsync();
        if (instalador != null)
        {
            NombreEntry.Text = instalador.Nombre;
            EmpresaEntry.Text = instalador.Empresa;
            CifNifEntry.Text = instalador.CifNif;
            DireccionEntry.Text = instalador.Direccion;
            TelefonoEntry.Text = instalador.Telefono;
            MailEntry.Text = instalador.Mail;
        }
    }
}