using LaCasaDelSueloRadianteApp;
using LaCasaDelSueloRadianteApp.Services;
using Microsoft.Maui.Storage;
using System;
using System.IO;
using System.Threading.Tasks;

namespace LaCasaDelSueloRadianteApp
{
    public partial class AgregarPage : ContentPage
    {
        private readonly DatabaseService _database;
        private readonly OneDriveService _oneDriveService;

        // Variables para almacenar las URLs de OneDrive de las fotos.
        private string? _phFotoUrl;
        private string? _conductividadFotoUrl;
        private string? _concentracionFotoUrl;
        private string? _turbidezFotoUrl;

        public AgregarPage(DatabaseService databaseService, OneDriveService oneDriveService)
        {
            InitializeComponent();
            _database = databaseService ?? throw new ArgumentNullException(nameof(databaseService));
            _oneDriveService = oneDriveService ?? throw new ArgumentNullException(nameof(oneDriveService));
        }

        private async void OnAdjuntarPhFotoClicked(object sender, EventArgs e)
        {
            await AdjuntarFotoAsync("pH", url => _phFotoUrl = url);
        }

        private async void OnAdjuntarConductividadFotoClicked(object sender, EventArgs e)
        {
            await AdjuntarFotoAsync("conductividad", url => _conductividadFotoUrl = url);
        }

        private async void OnAdjuntarConcentracionFotoClicked(object sender, EventArgs e)
        {
            await AdjuntarFotoAsync("concentracion", url => _concentracionFotoUrl = url);
        }

        private async void OnAdjuntarTurbidezFotoClicked(object sender, EventArgs e)
        {
            await AdjuntarFotoAsync("turbidez", url => _turbidezFotoUrl = url);
        }

        // Muestra un menú para elegir la fuente de la foto y retorna el FileResult obtenido
        private async Task<FileResult?> SeleccionarFotoAsync()
        {
            string action = await DisplayActionSheet(
                "Selecciona la fuente de la foto",
                "Cancelar",
                null,
                "Cámara",
                "Galería",
                "Carpeta");

            if (action == "Cancelar")
            {
                return null;
            }

            if (action == "Cámara")
            {
                if (MediaPicker.Default.IsCaptureSupported)
                {
                    try
                    {
                        return await MediaPicker.Default.CapturePhotoAsync();
                    }
                    catch (Exception ex)
                    {
                        await DisplayAlert("Error", $"No se pudo tomar la foto: {ex.Message}", "OK");
                        return null;
                    }
                }
                else
                {
                    await DisplayAlert("Error", "La captura de fotos no está soportada en este dispositivo", "OK");
                    return null;
                }
            }
            else if (action == "Galería")
            {
                try
                {
                    return await MediaPicker.Default.PickPhotoAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"No se pudo seleccionar la foto de la galería: {ex.Message}", "OK");
                    return null;
                }
            }
            else if (action == "Carpeta")
            {
                try
                {
                    var options = new PickOptions
                    {
                        PickerTitle = "Selecciona una imagen",
                        FileTypes = FilePickerFileType.Images
                    };
                    return await FilePicker.Default.PickAsync(options);
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"No se pudo seleccionar la foto: {ex.Message}", "OK");
                    return null;
                }
            }
            return null;
        }

        // Toma, sube y asigna la URL resultante de la foto.
        private async Task AdjuntarFotoAsync(string tipo, Action<string> setUrl)
        {
            try
            {
                var photo = await SeleccionarFotoAsync();
                if (photo != null)
                {
                    var result = await UploadPhotoToOneDriveAsync(photo, tipo);
                    setUrl(result.ShareUrl);
                    await DisplayAlert("Éxito", $"Foto de {tipo} guardada", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo tomar/guardar la foto: {ex.Message}", "OK");
            }
        }

        private async void OnGuardarClicked(object sender, EventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(NombreEntry.Text))
                {
                    await DisplayAlert("Error", "El nombre del cliente es requerido", "OK");
                    return;
                }

                // Crear y guardar la información básica del cliente.
                var cliente = new Cliente
                {
                    NombreCliente = NombreEntry.Text,
                    Direccion = DireccionEntry.Text,
                    Email = EmailEntry.Text,
                    Telefono = TelefonoEntry.Text
                };

                await _database.GuardarClienteAsync(cliente);

                // Crear y guardar el servicio asociado al cliente.
                var servicio = new Servicio
                {
                    ClienteId = cliente.Id,
                    Fecha = DateTime.Now,
                    TipoServicio = TipoServicioPicker.SelectedItem?.ToString(),
                    TipoInstalacion = TipoInstalacionPicker.SelectedItem?.ToString(),
                    FuenteCalor = FuenteCalorPicker.SelectedItem?.ToString(),
                    ValorPh = !string.IsNullOrEmpty(PhEntry.Text) ? double.Parse(PhEntry.Text) : (double?)null,
                    ValorConductividad = !string.IsNullOrEmpty(ConductividadEntry.Text) ? double.Parse(ConductividadEntry.Text) : (double?)null,
                    ValorConcentracion = !string.IsNullOrEmpty(ConcentracionInhibidorEntry.Text) ? double.Parse(ConcentracionInhibidorEntry.Text) : (double?)null,
                    ValorTurbidez = !string.IsNullOrEmpty(TurbidezEntry.Text) ? double.Parse(TurbidezEntry.Text) : (double?)null,
                    FotoPhUrl = _phFotoUrl,
                    FotoConductividadUrl = _conductividadFotoUrl,
                    FotoConcentracionUrl = _concentracionFotoUrl,
                    FotoTurbidezUrl = _turbidezFotoUrl
                };

                await _database.GuardarServicioAsync(servicio);

                // Sincronizar la base de datos (se sube un archivo JSON a OneDrive dentro de la carpeta)
                await _database.SincronizarConOneDriveAsync();

                await DisplayAlert("Éxito", "Servicio guardado y sincronizado", "OK");
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo guardar el servicio: {ex.Message}", "OK");
            }
        }

        // Sube la foto a OneDrive y retorna un ShareUrl simulado.
        private async Task<(string ItemId, string ShareUrl)> UploadPhotoToOneDriveAsync(FileResult photo, string photoType)
        {
            try
            {
                var fileName = $"{photoType}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(photo.FileName)}";
                await using var fileStream = await photo.OpenReadAsync();
                await _oneDriveService.UploadFileAsync(fileName, fileStream);
                // Ejemplo de ShareUrl formado (actualiza los valores según tu configuración).
                var shareUrl = $"https://onedrive.live.com/?cid=YOUR_ONEDRIVE_CID&resid=YOUR_ONEDRIVE_RESID&path=/lacasadelsueloradianteapp/{fileName}";
                return (fileName, shareUrl);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo subir la foto a OneDrive: {ex.Message}", "OK");
                throw;
            }
        }
    }
}