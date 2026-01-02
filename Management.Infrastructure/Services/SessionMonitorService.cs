using Management.Domain.Services;
using Management.Application.Services;
using Microsoft.Extensions.Logging;
using Management.Application.Services;
using System;
using Management.Application.Services;
using System.Threading;
using Management.Application.Services;
using System.Threading.Tasks;
using Management.Application.Services;

namespace Management.Infrastructure.Services
{
    public class SessionMonitorService : ISessionMonitorService
    {
        private readonly ISessionStorageService _sessionStorage;
        private readonly IAuthenticationService _authService;
        private readonly ILogger<SessionMonitorService> _logger;
        private Timer? _monitorTimer;
        private bool _isMonitoring;

        public event EventHandler<SessionExpiredEventArgs>? SessionExpired;
        public event EventHandler? SessionRefreshed;

        public SessionMonitorService(
            ISessionStorageService sessionStorage,
            IAuthenticationService authService,
            ILogger<SessionMonitorService> logger)
        {
            _sessionStorage = sessionStorage;
            _authService = authService;
            _logger = logger;
        }

        public Task StartMonitoringAsync()
        {
            if (_isMonitoring)
            {
                _logger.LogWarning("Session monitoring is already active");
                return Task.CompletedTask;
            }

            _logger.LogInformation("Starting session monitoring");
            _isMonitoring = true;

            // Check every 60 seconds
            _monitorTimer = new Timer(
                async _ => await CheckSessionAsync(),
                null,
                TimeSpan.Zero,
                TimeSpan.FromSeconds(60)
            );

            return Task.CompletedTask;
        }

        public Task StopMonitoringAsync()
        {
            if (!_isMonitoring)
            {
                return Task.CompletedTask;
            }

            _logger.LogInformation("Stopping session monitoring");
            _isMonitoring = false;
            _monitorTimer?.Dispose();
            _monitorTimer = null;

            return Task.CompletedTask;
        }

        private async Task CheckSessionAsync()
        {
            try
            {
                var session = await _sessionStorage.LoadSessionAsync();
                
                if (session == null)
                {
                    _logger.LogDebug("No session to monitor");
                    return;
                }

                if (session.IsExpired)
                {
                    _logger.LogWarning("Session has expired");
                    OnSessionExpired("Your session has expired. Please log in again.");
                    await StopMonitoringAsync();
                    return;
                }

                // If expiring soon, try to refresh
                if (session.IsExpiringSoon)
                {
                    _logger.LogInformation("Session expiring soon, attempting refresh");
                    var refreshResult = await _authService.RefreshSessionAsync();
                    
                    if (refreshResult.IsSuccess)
                    {
                        _logger.LogInformation("Session refreshed successfully");
                        SessionRefreshed?.Invoke(this, EventArgs.Empty);
                    }
                    else
                    {
                        _logger.LogError("Failed to refresh session: {Error}", refreshResult.Error.Message);
                        OnSessionExpired("Your session could not be refreshed. Please log in again.");
                        await StopMonitoringAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during session monitoring");
            }
        }

        private void OnSessionExpired(string message)
        {
            SessionExpired?.Invoke(this, new SessionExpiredEventArgs { Message = message });
        }
    }
}
