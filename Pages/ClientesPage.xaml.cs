using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp;

public partial class ClientesPage : ContentPage
{
    private readonly DatabaseService _db;

    public ObservableCollection<Cliente> Clientes { get; set; } = new();

    public ClientesPage()
    {
        InitializeComponent();

        // Recuperar DatabaseService desde el contenedor de servicios
        _db = App.Services.GetService<DatabaseService>()
              ?? throw new InvalidOperationException("No se pudo obtener la instancia de DatabaseService.");
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        await CargarClientesAsync();
    }

    private async Task CargarClientesAsync()
    {
        try
        {
            var clientes = await _db.ObtenerClientesAsync();
            Clientes.Clear();
            foreach (var cliente in clientes)
            {
                Clientes.Add(cliente);
            }

            ClientesCollectionView.ItemsSource = Clientes;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Error al cargar los clientes: {ex.Message}", "OK");
        }
    }

    private async void OnClienteSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is Cliente clienteSeleccionado)
        {
            // Navegar a la página de servicios del cliente seleccionado
            await Navigation.PushAsync(new ServiciosPage(clienteSeleccionado));
        }

        // Deseleccionar el cliente después de la navegación
        ((CollectionView)sender).SelectedItem = null;
    }
}