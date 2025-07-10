using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.Controls;
using LaCasaDelSueloRadianteApp.Services;
using LaCasaDelSueloRadianteApp.Models;

namespace LaCasaDelSueloRadianteApp;

public partial class ServiciosPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly Cliente _cliente;
    private readonly IImageService _imgSvc;

    public ObservableCollection<Servicio> Servicios { get; } = new();

    // Si usas filtrado, puedes exponer una propiedad ServiciosFiltrados
    public ObservableCollection<Servicio> ServiciosFiltrados => Servicios;

    public ICommand NavegarADetalleCommand { get; }

    public ServiciosPage(Cliente cliente)
    {
        InitializeComponent();

        _cliente = cliente;
        Title = $"Servicios de {cliente.NombreCliente}";

        _db = App.Services.GetService<DatabaseService>()
              ?? throw new InvalidOperationException("No se pudo obtener la instancia de DatabaseService.");

        _imgSvc = App.Services.GetService<IImageService>()
                 ?? throw new InvalidOperationException("No se pudo obtener la instancia de IImageService.");

        NavegarADetalleCommand = new Command<Servicio>(async (servicio) =>
        {
            if (servicio != null)
            {
                var instalador = await _db.ObtenerInstaladorAsync();
                if (instalador == null)
                {
                    await Application.Current.MainPage.DisplayAlert("Error", "No se encontró información del instalador.", "OK");
                    return;
                }
                await Navigation.PushAsync(new ServicioDetallePage(servicio, _cliente, instalador, _imgSvc));
            }
        });

        BindingContext = this;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await CargarServiciosAsync();
    }

    private async Task CargarServiciosAsync()
    {
        try
        {
            var servicios = await _db.ObtenerServiciosAsync(_cliente.Id);
            Servicios.Clear();
            foreach (var servicio in servicios)
                Servicios.Add(servicio);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error al cargar los servicios: {ex.Message}", "OK");
        }
    }
}