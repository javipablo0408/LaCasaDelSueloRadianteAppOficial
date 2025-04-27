using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp;

public partial class ServiciosPage : ContentPage
{
    private readonly DatabaseService _db;
    private readonly Cliente _cliente;

    public ObservableCollection<Servicio> Servicios { get; set; } = new();

    public ServiciosPage(Cliente cliente)
    {
        InitializeComponent();

        _cliente = cliente;
        Title = $"Servicios de {cliente.NombreCliente}";

        // Recuperar DatabaseService desde el contenedor de servicios
        _db = App.Services.GetService<DatabaseService>()
              ?? throw new InvalidOperationException("No se pudo obtener la instancia de DatabaseService.");
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
            {
                Servicios.Add(servicio);
            }

            ServiciosCollectionView.ItemsSource = Servicios;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error al cargar los servicios: {ex.Message}", "OK");
        }
    }

    private async void OnServicioSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Servicio servicioSeleccionado)
        {
            // Depuración: Verificar los datos del servicio seleccionado
            System.Diagnostics.Debug.WriteLine($"Servicio seleccionado: {servicioSeleccionado.TipoServicio}");
            System.Diagnostics.Debug.WriteLine($"FotoPhUrl: {servicioSeleccionado.FotoPhUrl}");

            // Navegar a la página de detalles del servicio seleccionado
            await Navigation.PushAsync(new ServicioDetallePage(servicioSeleccionado));
        }

    // Deseleccionar el servicio después de la navegación
    ((CollectionView)sender).SelectedItem = null;
    }
}