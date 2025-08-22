using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using System;
using System.Threading.Tasks;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp.Pages;

public partial class CodigoPremiumPage : ContentPage
{
    private readonly PremiumCodeService _premiumCodeService;

    public CodigoPremiumPage()
    {
        InitializeComponent();
        // Usar la misma instancia que PremiumHelper para evitar problemas de estado
        _premiumCodeService = PremiumHelper.GetPremiumService();
        CargarEstadoActual();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        CargarEstadoActual();
    }

    private void CargarEstadoActual()
    {
        var estadoPremium = _premiumCodeService.ObtenerEstadoPremium();
        ActualizarInterfazEstado(estadoPremium);
    }

    private void ActualizarInterfazEstado(EstadoPremium estado)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (estado.EsActivo)
            {
                EstadoIcono.Text = "✅";
                EstadoTexto.Text = "Código premium activo";
                EstadoTexto.TextColor = Colors.Green;
                FechaExpiracionLabel.Text = $"Expira el: {estado.FechaExpiracion:dd/MM/yyyy HH:mm}";
                FechaExpiracionLabel.IsVisible = true;
                EstadoFrame.BackgroundColor = Color.FromArgb("#E8F5E8");
            }
            else
            {
                EstadoIcono.Text = "❌";
                EstadoTexto.Text = "Sin código premium activo";
                EstadoTexto.TextColor = Colors.Red;
                FechaExpiracionLabel.IsVisible = false;
                EstadoFrame.BackgroundColor = Color.FromArgb("#FFE8E8");
                
                if (!string.IsNullOrEmpty(estado.MensajeEstado))
                {
                    EstadoTexto.Text = estado.MensajeEstado;
                }
            }
        });
    }

    private async void OnValidarCodigoClicked(object sender, EventArgs e)
    {
        var codigo = CodigoEntry.Text?.Trim().ToUpper();
        
        if (string.IsNullOrEmpty(codigo))
        {
            await MostrarMensaje("Por favor, introduce un código", Colors.Orange);
            return;
        }

        if (codigo.Length < 5)
        {
            await MostrarMensaje("El código debe tener al menos 5 caracteres", Colors.Orange);
            return;
        }

        await EjecutarConCarga(async () =>
        {
            try
            {
                var resultado = await _premiumCodeService.ValidarCodigoAsync(codigo);
                
                if (resultado.EsExitoso)
                {
                    await MostrarMensaje("¡Código activado correctamente! 🎉", Colors.Green);
                    CodigoEntry.Text = string.Empty;
                    CargarEstadoActual();
                }
                else
                {
                    await MostrarMensaje(resultado.Mensaje, Colors.Red);
                }
            }
            catch (Exception ex)
            {
                await MostrarMensaje("Error de conexión. Verifica tu conexión a internet.", Colors.Red);
                System.Diagnostics.Debug.WriteLine($"Error validando código: {ex.Message}");
            }
        });
    }

    private async void OnVerificarEstadoClicked(object sender, EventArgs e)
    {
        await EjecutarConCarga(async () =>
        {
            try
            {
                await _premiumCodeService.VerificarEstadoRemoroAsync();
                CargarEstadoActual();
                await MostrarMensaje("Estado actualizado", Colors.Blue);
            }
            catch (Exception ex)
            {
                await MostrarMensaje("Error al verificar estado", Colors.Red);
                System.Diagnostics.Debug.WriteLine($"Error verificando estado: {ex.Message}");
            }
        });
    }

    private async void OnIrAWebClicked(object sender, EventArgs e)
    {
        try
        {
            await Launcher.Default.OpenAsync("https://lacasadelsueloradiante.es");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", "No se pudo abrir el navegador", "OK");
            System.Diagnostics.Debug.WriteLine($"Error abriendo navegador: {ex.Message}");
        }
    }

    private async Task EjecutarConCarga(Func<Task> accion)
    {
        LoadingIndicator.IsVisible = true;
        LoadingIndicator.IsRunning = true;
        ValidarButton.IsEnabled = false;
        VerificarButton.IsEnabled = false;

        try
        {
            await accion();
        }
        finally
        {
            LoadingIndicator.IsVisible = false;
            LoadingIndicator.IsRunning = false;
            ValidarButton.IsEnabled = true;
            VerificarButton.IsEnabled = true;
        }
    }

    private async Task MostrarMensaje(string mensaje, Color color)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MensajeLabel.Text = mensaje;
            MensajeLabel.TextColor = color;
            MensajeLabel.IsVisible = true;
        });

        // Ocultar el mensaje después de 4 segundos
        await Task.Delay(4000);
        
        MainThread.BeginInvokeOnMainThread(() =>
        {
            MensajeLabel.IsVisible = false;
        });
    }
}
