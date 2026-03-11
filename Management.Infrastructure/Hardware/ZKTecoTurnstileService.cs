using System;
using System.Threading.Tasks;
using Management.Domain.Events;
using Management.Domain.Services;
using Management.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces;

namespace Management.Infrastructure.Hardware
{
    public class ZKTecoTurnstileService : IHardwareTurnstileService
    {
        private dynamic? _zk;
        private readonly TurnstileConfig _config;
        private readonly ILogger<ZKTecoTurnstileService> _logger;
        private readonly IHardwareService _hardwareService;
        private bool _isDisposed;
        private bool _isConnecting;
        private int _consecutiveFailures;
        private const int MaxFailuresBeforeAlert = 3;
        public bool IsSdkAvailable { get; private set; }

        public event EventHandler<TurnstileScanEventArgs>? CardScanned;
        public event Action<bool>? ConnectionStatusChanged;

        public bool IsConnected { get; private set; }
        public string DeviceIp => _config.IpAddress;
        public int DevicePort => _config.Port;

        public ZKTecoTurnstileService(
            TurnstileConfig config, 
            ILogger<ZKTecoTurnstileService> logger,
            IHardwareService hardwareService)
        {
            _config = config;
            _logger = logger;
            _hardwareService = hardwareService;
            
            try
            {
                Type? type = Type.GetTypeFromProgID("zkemkeeper.ZKEM.1");
                if (type != null)
                {
                    _zk = Activator.CreateInstance(type);
                    IsSdkAvailable = true;
                    _logger.LogInformation("ZKTeco SDK initialized successfully.");
                }
                else
                {
                    IsSdkAvailable = false;
                    _logger.LogError("ZKTeco SDK (zkemkeeper.ZKEM.1) not found in registry. Ensure SDK is registered.");
                    _hardwareService.NotifyStatusChanged("Turnstile", false, "SDK_MISSING");
                }
            }
            catch (Exception ex)
            {
                IsSdkAvailable = false;
                _logger.LogError(ex, "Failed to initialize ZKTeco SDK.");
                _hardwareService.NotifyStatusChanged("Turnstile", false, "SDK_INIT_FAILED");
            }
        }

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            if (_zk == null || _isConnecting) return false;
            _isConnecting = true;

            try
            {
                _logger.LogInformation("Connecting to ZKTeco Turnstile: {Ip}:{Port}...", ip, port);
                
                bool connected = false;
                int attempts = 0;
                while (!connected && attempts < 3)
                {
                    attempts++;
                    connected = _zk.Connect_Net(ip, port);
                    if (!connected)
                    {
                        _logger.LogWarning("Connection attempt {Attempt} failed. Retrying...", attempts);
                        await Task.Delay(2000);
                    }
                }

                if (connected)
                {
                    IsConnected = true;
                    _zk.RegEvent(_config.MachineNumber, 65535);
                    
                    // Wire events for modern ZK SDK
                    _zk.OnAttTransactionEx += new Action<string, int, int, int, int, int, int, int, int, int, int>(Zk_OnAttTransactionEx);

                    _logger.LogInformation("Connected successfully to ZKTeco machine {MachineNumber}", _config.MachineNumber);
                    _hardwareService.NotifyStatusChanged("Turnstile", true);
                    ConnectionStatusChanged?.Invoke(true);
                }
                else
                {
                    _logger.LogError("Failed to connect to ZKTeco Turnstile after {Attempts} attempts.", attempts);
                    _hardwareService.NotifyStatusChanged("Turnstile", false, "Connection failed");
                }

                return connected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during ZKTeco connection.");
                return false;
            }
            finally
            {
                _isConnecting = false;
            }
        }

        public void StartMonitoring()
        {
            _logger.LogInformation("Real-time monitoring enabled.");
        }

        public async Task<bool> OpenGateAsync()
        {
            if (!IsConnected || _zk == null) return false;

            try
            {
                bool result = await Task.Run(() => (bool)_zk.ACUnlock(DevicePort, _config.GateOpenDurationMs));
                
                if (result)
                {
                    _consecutiveFailures = 0;
                    _logger.LogInformation("Gate opened successfully.");
                }
                else
                {
                    _consecutiveFailures++;
                    _logger.LogWarning("Gate open command failed. Failure count: {Count}", _consecutiveFailures);
                    CheckFailureThreshold();
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _consecutiveFailures++;
                _logger.LogError(ex, "Exception during Gate Open. Failure count: {Count}", _consecutiveFailures);
                CheckFailureThreshold();
                return false;
            }
        }

        public async Task<bool> PingAsync()
        {
            if (!IsConnected || _zk == null) return false;

            try
            {
                // GetDeviceTime is a lightweight call to check if the connection is alive
                int idw = 0, iyear = 0, imonth = 0, iday = 0, ihour = 0, iminute = 0, isecond = 0;
                bool result = await Task.Run(() => (bool)_zk.GetDeviceTime(DevicePort, ref iyear, ref imonth, ref iday, ref ihour, ref iminute, ref isecond));
                
                if (!result)
                {
                    _logger.LogWarning("Health Check: Ping failed.");
                    _consecutiveFailures++;
                    CheckFailureThreshold();
                }
                else
                {
                    _consecutiveFailures = 0;
                }
                
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Health Check: Ping threw exception.");
                _consecutiveFailures++;
                CheckFailureThreshold();
                return false;
            }
        }

        private void CheckFailureThreshold()
        {
            if (_consecutiveFailures >= MaxFailuresBeforeAlert)
            {
                _logger.LogCritical("Turnstile hardware reached consecutive failure threshold! Notifying UI.");
                ConnectionStatusChanged?.Invoke(false);
                _hardwareService.NotifyStatusChanged("Turnstile", false, "STABILITY_ISSUE");
            }
        }
        private void Zk_OnAttTransactionEx(string enrollNumber, int isInValid, int attState, int verifyMethod, int year, int month, int day, int hour, int minute, int second, int workCode)
        {
            _logger.LogInformation("Hardware Activity: RawData={RawData}, Valid={Valid}", enrollNumber, isInValid == 0);
            
            string cardId = enrollNumber;
            string deviceName = "SATT-MAIN";
            string transactionId = $"TXN-{DateTime.UtcNow.Ticks}";

            // Fix 1: Parse comma-separated data if present
            // Format: "200001742,SATT-MAIN,CD11301121"
            if (!string.IsNullOrEmpty(enrollNumber) && enrollNumber.Contains(","))
            {
                var parts = enrollNumber.Split(',');
                if (parts.Length >= 1) cardId = parts[0].Trim();
                if (parts.Length >= 2) deviceName = parts[1].Trim();
                if (parts.Length >= 3) transactionId = parts[2].Trim();
                
                _logger.LogDebug("Parsed Scan: CardId={CardId}, Device={Device}, TxId={TxId}", cardId, deviceName, transactionId);
            }

            var timestamp = new DateTime(year, month, day, hour, minute, second);
            var args = new TurnstileScanEventArgs(cardId, deviceName, transactionId, isInValid == 0, verifyMethod, timestamp);
            
            CardScanned?.Invoke(this, args);
        }

        public async Task DisconnectAsync()
        {
            if (_zk != null && IsConnected)
            {
                _zk.Disconnect();
                IsConnected = false;
                _hardwareService.NotifyStatusChanged("Turnstile", false);
                ConnectionStatusChanged?.Invoke(false);
                _logger.LogInformation("Disconnected from ZKTeco device.");
            }
            await Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            
            if (_zk != null)
            {
                try { _zk.OnAttTransactionEx -= new Action<string, int, int, int, int, int, int, int, int, int, int>(Zk_OnAttTransactionEx); } catch { }
                if (IsConnected) _zk.Disconnect();
            }

            _isDisposed = true;
        }
    }
}
