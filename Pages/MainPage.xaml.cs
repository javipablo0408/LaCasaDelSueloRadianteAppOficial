using System.Collections.ObjectModel;
using System.Windows.Input;
using Microsoft.Maui.ApplicationModel; // Para Launcher
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp;

public partial class MainPage : ContentPage
{
    public ObservableCollection<Producto> Productos { get; set; }
    public ICommand ComprarCommand { get; }

    public MainPage()
    {
        InitializeComponent();

        Productos = new ObservableCollection<Producto>
        {
            new Producto
            {
                Nombre = "X100 Inhibidor",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2024/07/sentinel-x100_1expb-1-scaled.jpg",
                UrlCompra = "https://lacasadelsueloradiante.es/product/x100-inhibidor/"
            },
            new Producto
            {
                Nombre = "X800 Limpiador de accion rapida 1L",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2024/07/sentinel-x800-1expa-scaled.jpg",
                UrlCompra = "https://lacasadelsueloradiante.es/product/x800l-1l-x800-rapid-cleaner-1l/"
            },
            new Producto
            {
                Nombre = "X700 Biocida 1L",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2024/07/sentinel-x700-1expb-scaled.jpg",
                UrlCompra = "https://lacasadelsueloradiante.es/product/x700-biocida-1l/"
            },
            new Producto
            {
                Nombre = "X100 Rapid-Dose Inhibidor",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2024/07/sentinel-x100-rdexp-scaled.jpg",
                UrlCompra = "https://lacasadelsueloradiante.es/product/x100-rapid-dose-inhibidor/"
            },
            new Producto
            {
                Nombre = "DS40 Descaler & Cleaner 2kg",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2025/01/DS40.jpg",
                UrlCompra = "https://lacasadelsueloradiante.es/product/ds40-descaler-cleaner-2kg/"
            },
            new Producto
            {
                Nombre = "FLOIXEM C 10LITROS LIMPIADOR SISTEMAS DE CALEFACCION",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2024/11/FLOIXEM-10L-C-verde-10Kg.webp",
                UrlCompra = "https://lacasadelsueloradiante.es/product/floixem-c-10litros-limpiador-sistemas-de-calefaccion/"
            },
            new Producto
            {
                Nombre = "FLOIXEM I Inhibidor circuito de calefaccion 1L",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2024/11/Floixem-I-Lateral-1000x1000-1.png",
                UrlCompra = "https://lacasadelsueloradiante.es/product/floixem-i-inhibidor-circuito-de-calefaccion-1l/"
            },
            new Producto
            {
                Nombre = "FLOIXEM B  1L. BIOCIODA DE AMPLIO ESPECTRO",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2024/11/Floixem-B-Lateral-1000x1000-2.png",
                UrlCompra = "https://lacasadelsueloradiante.es/product/floixem-b-1l-biocioda-de-alto-espectro/"
            },
            new Producto
            {
                Nombre = "FLOIXEM I Inhibidor circuito de calefaccion 10L",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2024/11/FLOIXEM-I-Inhibidor-circuito-de-calefaccion-10L.png",
                UrlCompra = "https://lacasadelsueloradiante.es/product/floixem-i-inhibidor-circuito-de-calefaccion-10l/"
            },
            new Producto
            {
                Nombre = "FERNOX F1 INHIBIDOR 265ml",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2025/01/Diseno-sin-titulo-6.png",
                UrlCompra = "https://lacasadelsueloradiante.es/product/fernox-f1-inhibidor/"
            },
            new Producto
            {
                Nombre = "LS-1L – Leak Sealer 1L",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2024/07/sentinel-leak_sealer-1expb-scaled.jpg",
                UrlCompra = "https://lacasadelsueloradiante.es/product/ls-1l-leak-sealer-1l/"
            },
            new Producto
            {
                Nombre = "FLOIXEM B  10L. BIOCIODA DE AMPLIO ESPECTRO",
                Precio = 0,
                ImagenUrl = "https://lacasadelsueloradiante.es/wp-content/uploads/2024/11/ads.png",
                UrlCompra = "https://lacasadelsueloradiante.es/product/floixem-b-10litros-biocioda-de-alto-espectro-circuito-calefaccion/"
            }
        };

        ComprarCommand = new Command<Producto>(async producto =>
        {
            if (!string.IsNullOrWhiteSpace(producto?.UrlCompra))
                await Launcher.Default.OpenAsync(producto.UrlCompra);
        });

        BindingContext = this;
    }
}