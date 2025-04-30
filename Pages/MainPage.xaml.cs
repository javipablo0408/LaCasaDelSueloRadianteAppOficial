using System.Collections.ObjectModel;
using System.Windows.Input;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp;

public partial class MainPage : ContentPage
{
    public ObservableCollection<Producto> Productos { get; set; }
    public ICommand AñadirAlCarritoCommand { get; }

    public MainPage()
    {
        InitializeComponent();

        // Lista de productos
        Productos = new ObservableCollection<Producto>
        {
            new Producto
            {
                Nombre = "Express Inhibitor Test",
                Precio = 38.50m,
                ImagenUrl = "https://via.placeholder.com/150",
                UrlCompra = "https://miweb.com/producto1"
            },
            new Producto
            {
                Nombre = "FERNOX F1 EXPRESS INHIBIDOR",
                Precio = 45.92m,
                ImagenUrl = "https://via.placeholder.com/150",
                UrlCompra = "https://miweb.com/producto2"
            },
            new Producto
            {
                Nombre = "FERNOX F1 INHIBIDOR 265ml",
                Precio = 39.71m,
                ImagenUrl = "https://via.placeholder.com/150",
                UrlCompra = "https://miweb.com/producto3"
            }
        };

        // Comando para añadir al carrito
        AñadirAlCarritoCommand = new Command<Producto>(producto =>
        {
            // Lógica para añadir al carrito
            DisplayAlert("Carrito", $"{producto.Nombre} añadido al carrito.", "OK");
        });

        BindingContext = this;
    }
}