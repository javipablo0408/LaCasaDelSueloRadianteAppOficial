using Microsoft.Maui.Storage;
using System.Collections.ObjectModel;

namespace LaCasaDelSueloRadianteApp
{
    public partial class AgregarPage : ContentPage
    {
        private readonly DatabaseService _database;
        private readonly GraphService _graphService;

        // Variables para almacenar las rutas locales de las fotos
        private string? _phFotoPath;
        private string? _conductividadFotoPath;
        private string? _concentracionFotoPath;
        private string? _turbidezFotoPath;

        // Variables para almacenar las URLs de OneDrive
        private string? _phFotoUrl;
        private string? _conductividadFotoUrl;
        private string? _concentracionFotoUrl;
        private string? _turbidezFotoUrl;

        public AgregarPage(GraphService graphService)
        {
            InitializeComponent();
            _graphService = graphService ?? throw new ArgumentNullException(nameof(graphService));
            string dbPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "clientes.db3");
            _database = new DatabaseService(dbPath);
        }

        private async void OnAdjuntarPhFotoClicked(object sender, EventArgs e)
        {
            try
            {
                var photo = await TakePhotoAsync();
                if (photo != null)
                {
                    _phFotoPath = photo.FullPath;
                    var result = await UploadPhotoToOneDriveAsync(_phFotoPath, "pH");
                    _phFotoUrl = result.ShareUrl;
                    await DisplayAlert("Éxito", "Foto de pH guardada", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo tomar/guardar la foto: {ex.Message}", "OK");
            }
        }

        private async void OnAdjuntarConductividadFotoClicked(object sender, EventArgs e)
        {
            try
            {
                var photo = await TakePhotoAsync();
                if (photo != null)
                {
                    _conductividadFotoPath = photo.FullPath;
                    var result = await UploadPhotoToOneDriveAsync(_conductividadFotoPath, "conductividad");
                    _conductividadFotoUrl = result.ShareUrl;
                    await DisplayAlert("Éxito", "Foto de conductividad guardada", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo tomar/guardar la foto: {ex.Message}", "OK");
            }
        }

        private async void OnAdjuntarConcentracionFotoClicked(object sender, EventArgs e)
        {
            try
            {
                var photo = await TakePhotoAsync();
                if (photo != null)
                {
                    _concentracionFotoPath = photo.FullPath;
                    var result = await UploadPhotoToOneDriveAsync(_concentracionFotoPath, "concentracion");
                    _concentracionFotoUrl = result.ShareUrl;
                    await DisplayAlert("Éxito", "Foto de concentración guardada", "OK");
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo tomar/guardar la foto: {ex.Message}", "OK");
            }
        }

        private async void OnAdjuntarTurbidezFotoClicked(object sender, EventArgs e)
        {
            try
            {
                var photo = await TakePhotoAsync();
                if (photo != null)
                {
                    _turbidezFotoPath = photo.FullPath;
                    var result = await UploadPhotoToOneDriveAsync(_turbidezFotoPath, "turbidez");
                    _turbidezFotoUrl = result.ShareUrl;
                    await DisplayAlert("Éxito", "Foto de turbidez guardada", "OK");
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
                // Validar campos requeridos
                if (string.IsNullOrWhiteSpace(NombreEntry.Text))
                {
                    await DisplayAlert("Error", "El nombre del cliente es requerido", "OK");
                    return;
                }

                var servicio = new Servicio
                {
                    Fecha = DateTime.Now,
                    NombreCliente = NombreEntry.Text,
                    Direccion = DireccionEntry.Text,
                    Email = EmailEntry.Text,
                    Telefono = TelefonoEntry.Text,
                    TipoServicio = TipoServicioPicker.SelectedItem?.ToString(),
                    TipoInstalacion = TipoInstalacionPicker.SelectedItem?.ToString(),
                    FuenteCalor = FuenteCalorPicker.SelectedItem?.ToString(),
                    ValorPh = !string.IsNullOrEmpty(PhEntry.Text) ? double.Parse(PhEntry.Text) : null,
                    ValorConductividad = !string.IsNullOrEmpty(ConductividadEntry.Text) ? double.Parse(ConductividadEntry.Text) : null,
                    ValorConcentracion = !string.IsNullOrEmpty(ConcentracionInhibidorEntry.Text) ? double.Parse(ConcentracionInhibidorEntry.Text) : null,
                    ValorTurbidez = !string.IsNullOrEmpty(TurbidezEntry.Text) ? double.Parse(TurbidezEntry.Text) : null,
                    FotoPhUrl = _phFotoUrl,
                    FotoConductividadUrl = _conductividadFotoUrl,
                    FotoConcentracionUrl = _concentracionFotoUrl,
                    FotoTurbidezUrl = _turbidezFotoUrl
                };

                await _database.GuardarServicioAsync(servicio);
                await DisplayAlert("Éxito", "Servicio guardado correctamente", "OK");
                await Navigation.PopAsync();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo guardar el servicio: {ex.Message}", "OK");
            }
        }

        private async Task<FileResult?> TakePhotoAsync()
        {
            if (MediaPicker.Default.IsCaptureSupported)
            {
                try
                {
                    var photo = await MediaPicker.Default.CapturePhotoAsync();
                    return photo;
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

        private async Task<(string ItemId, string ShareUrl)> UploadPhotoToOneDriveAsync(string localPath, string photoType)
        {
            try
            {
                var fileName = $"{photoType}_{DateTime.Now:yyyyMMddHHmmss}{Path.GetExtension(localPath)}";
                return await _graphService.UploadFileToOneDriveAsync(localPath, fileName);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error", $"No se pudo subir la foto a OneDrive: {ex.Message}", "OK");
                throw;
            }
        }
    }
}