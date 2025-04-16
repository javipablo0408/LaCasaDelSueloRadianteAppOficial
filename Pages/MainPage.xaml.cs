using System.Collections.ObjectModel;
using LaCasaDelSueloRadianteApp.Services;

namespace LaCasaDelSueloRadianteApp
{
    public partial class MainPage : ContentPage
    {
        private readonly OneDriveService _oneDriveService;
        private OneDriveService.DriveItem _selectedItem;
        private ObservableCollection<OneDriveService.DriveItem> _files;

        public MainPage(OneDriveService oneDriveService)
        {
            InitializeComponent();
            _oneDriveService = oneDriveService;
            _files = new ObservableCollection<OneDriveService.DriveItem>();
            FilesCollection.ItemsSource = _files;

            LoadFiles();
        }

        private async void LoadFiles()
        {
            if (IsBusy) return;

            try
            {
                IsBusy = true;
                _files.Clear();

                var files = await _oneDriveService.ListFilesAsync();
                foreach (var file in files)
                {
                    _files.Add(file);
                }
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    "No se pudieron cargar los archivos: " + ex.Message,
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void OnFileSelected(object sender, SelectionChangedEventArgs e)
        {
            _selectedItem = e.CurrentSelection.FirstOrDefault() as OneDriveService.DriveItem;
        }

        private async void OnUploadClicked(object sender, EventArgs e)
        {
            if (IsBusy) return;

            try
            {
                var result = await FilePicker.PickAsync();
                if (result == null) return;

                IsBusy = true;

                using var stream = await result.OpenReadAsync();
                await _oneDriveService.UploadFileAsync(result.FileName, stream);

                await DisplayAlert("Éxito", "Archivo subido correctamente", "OK");
                LoadFiles();
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    "Error al subir el archivo: " + ex.Message,
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnDownloadClicked(object sender, EventArgs e)
        {
            if (IsBusy) return;

            if (_selectedItem == null)
            {
                await DisplayAlert("Aviso",
                    "Por favor, seleccione un archivo para descargar",
                    "OK");
                return;
            }

            try
            {
                IsBusy = true;

                var stream = await _oneDriveService.DownloadFileAsync(_selectedItem.Id);
                var targetFile = Path.Combine(FileSystem.CacheDirectory, _selectedItem.Name);

                using (var fileStream = File.Create(targetFile))
                {
                    await stream.CopyToAsync(fileStream);
                }

                await Share.RequestAsync(new ShareFileRequest
                {
                    Title = _selectedItem.Name,
                    File = new ShareFile(targetFile)
                });
            }
            catch (Exception ex)
            {
                await DisplayAlert("Error",
                    "Error al descargar el archivo: " + ex.Message,
                    "OK");
            }
            finally
            {
                IsBusy = false;
            }
        }

        private async void OnLogoutClicked(object sender, EventArgs e)
        {
            if (IsBusy) return;

            var result = await DisplayAlert("Cerrar Sesión",
                "¿Está seguro que desea cerrar sesión?",
                "Sí", "No");

            if (result)
            {
                try
                {
                    IsBusy = true;
                    await _oneDriveService.SignOutAsync();
                    await Navigation.PopToRootAsync();
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error",
                        "Error al cerrar sesión: " + ex.Message,
                        "OK");
                }
                finally
                {
                    IsBusy = false;
                }
            }
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            LoadFiles();
        }
    }
}