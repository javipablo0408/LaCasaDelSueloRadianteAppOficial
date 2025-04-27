using Microsoft.Maui.Controls;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp;

public partial class ServicioDetallePage : ContentPage
{
    public ServicioDetallePage(Servicio servicio)
    {
        InitializeComponent();
        BindingContext = servicio;

        // Depuración: Verificar los datos del modelo
        System.Diagnostics.Debug.WriteLine($"TipoServicio: {servicio.TipoServicio}");
        System.Diagnostics.Debug.WriteLine($"ValorPh: {servicio.ValorPh}");
        System.Diagnostics.Debug.WriteLine($"FotoPhUrl: {servicio.FotoPhUrl}");
        System.Diagnostics.Debug.WriteLine($"FotoConductividadUrl: {servicio.FotoConductividadUrl}");
        System.Diagnostics.Debug.WriteLine($"FotoConcentracionUrl: {servicio.FotoConcentracionUrl}");
        System.Diagnostics.Debug.WriteLine($"FotoTurbidezUrl: {servicio.FotoTurbidezUrl}");
    }
}