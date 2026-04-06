using System;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Microsoft.Extensions.Logging;
using Management.Application.Notifications;
using Management.Application.Interfaces.App;
using System.Windows;

namespace Management.Presentation.Services
{
    public interface IUpdateService
    {
        Task CheckForUpdatesAsync(CancellationToken ct = default);
    }

    public class UpdateService : IUpdateService
    {
        private readonly ILogger<UpdateService> _logger;
        private readonly IToastService _toastService;

        // Change this to match the actual production URL (e.g. GitHub Releases, AWS S3 etc.)
        private const string UpdateServerUrl = "https://github.com/Luxurya/releases/releases/latest/download";

        public UpdateService(ILogger<UpdateService> logger, IToastService toastService)
        {
            _logger = logger;
            _toastService = toastService;
        }

        public async Task CheckForUpdatesAsync(CancellationToken ct = default)
        {
            try
            {
                var mgr = new UpdateManager(UpdateServerUrl);

                if (!mgr.IsInstalled)
                {
                    _logger.LogInformation("[Update] Application is not running from an installed context. Skipping updates.");
                    return;
                }

                _logger.LogInformation("[Update] Checking for updates silently...");
                
                var newVersion = await mgr.CheckForUpdatesAsync();
                
                if (newVersion == null)
                {
                    _logger.LogInformation("[Update] Application is up to date.");
                    return;
                }

                _logger.LogInformation("[Update] New version available: {Version}. Downloading silently...", newVersion.TargetFullRelease.Version);

                // Download silently in background
                await mgr.DownloadUpdatesAsync(newVersion, progress => 
                {
                    _logger.LogDebug("[Update] Download progress: {Progress}%", progress);
                });

                _logger.LogInformation("[Update] Downloaded successfully. Ready for next launch.");

                // Notify UI seamlessly
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _toastService.ShowInfo(
                        $"Update available: v{newVersion.TargetFullRelease.Version}. It will install seamlessly on your next restart.",
                        "Background Update");
                });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Update] Update check failed silently (network error or server down, totally fine to continue)");
            }
        }
    }
}
