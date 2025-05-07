using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Maui.Dispatching;

namespace LaCasaDelSueloRadianteApp.Services
{
    public class OneDriveSyncService : BackgroundService
    {
        private readonly OneDriveService _oneDriveService;
        private readonly TimeSpan _syncInterval = TimeSpan.FromMinutes(1);
        private readonly IDispatcher _dispatcher;

        public OneDriveSyncService(OneDriveService oneDriveService, IDispatcher dispatcher)
        {
            _oneDriveService = oneDriveService ?? throw new ArgumentNullException(nameof(oneDriveService));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await _oneDriveService.SincronizarBidireccionalAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error durante la sincronización: {ex.Message}");
                }

                await Task.Delay(_syncInterval, stoppingToken);
            }
        }

        public async Task SincronizarAhoraAsync(CancellationToken ct = default)
        {
            try
            {
                await _oneDriveService.SincronizarBidireccionalAsync(ct);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error durante la sincronización manual: {ex.Message}");
            }
        }
    }
}