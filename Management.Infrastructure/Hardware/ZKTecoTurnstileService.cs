using System;
using System.Threading;
using System.Threading.Tasks;
using Management.Domain.Events;
using Management.Domain.Services;
using Management.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Management.Application.Interfaces;

namespace Management.Infrastructure.Hardware
{
    public class ZKTecoTurnstileService : IHardwareTurnstileService, IDisposable
    {
        private readonly StaTaskRunner _sta = new();
        private dynamic? _zk; // Created and used ONLY on STA thread
        private readonly TurnstileConfig _config;
        private readonly ILogger<ZKTecoTurnstileService> _logger;
        private readonly IHardwareService _hardwareService;
        private bool _isDisposed;
        private bool _isConnecting;
        private int _consecutiveFailures;
        private const int MaxFailuresBeforeAlert = 3;
        
        private bool? _sdkAvailable;
        public bool IsSdkAvailable
        {
            get
            {
                if (_sdkAvailable.HasValue) return _sdkAvailable.Value;
                try
                {
                    var type = Type.GetTypeFromProgID("zkemkeeper.ZKEM.1");
                    _sdkAvailable = type != null;
                }
                catch
                {
                    _sdkAvailable = false;
                }
                return _sdkAvailable.Value;
            }
        }

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
            
            // Proactive SDK check
            if (IsSdkAvailable)
            {
                _logger.LogInformation("ZKTeco SDK detected in registry.");
            }
            else
            {
                _logger.LogError("ZKTeco SDK (zkemkeeper.ZKEM.1) not found in registry.");
                _hardwareService.NotifyStatusChanged("Turnstile", false, "SDK_MISSING");
            }
        }

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            if (!IsSdkAvailable || _isConnecting) 
            {
                if (!IsSdkAvailable) _logger.LogWarning("ZKTeco: Connection aborted. SDK not available.");
                return false;
            }
            _isConnecting = true;

            try
            {
                _logger.LogInformation("Connecting to ZKTeco Turnstile: {Ip}:{Port} (Timeout: 5s)...", ip, port);
                
                bool connected = false;
                int attempts = 0;
                while (!connected && attempts < 3)
                {
                    attempts++;
                    
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                    try
                    {
                        connected = await _sta.RunAsync(() =>
                        {
                            if (_zk == null)
                            {
                                var type = Type.GetTypeFromProgID("zkemkeeper.ZKEM.1");
                                if (type == null) return false;
                                _zk = Activator.CreateInstance(type);
                            }
                            
                            // Must be called before Connect_Net
                            // Default is 0 — no password. Reads from config if available.
                            _zk.SetCommPassword(_config.CommKey);
                            
                            return (bool)_zk!.Connect_Net(ip, port);
                        }).WaitAsync(cts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Connection attempt {Attempt} timed out after 5s.", attempts);
                        connected = false;
                    }

                    if (!connected && attempts < 3)
                    {
                        _logger.LogWarning("Connection attempt {Attempt} failed. Retrying...", attempts);
                        await Task.Delay(1000).ConfigureAwait(false);
                    }
                }

                if (connected)
                {
                    IsConnected = true;
                    await _sta.RunAsync(() =>
                    {
                        _zk!.RegEvent(_config.MachineNumber, 65535);
                        _zk.OnAttTransactionEx += new Action<string, int, int, int, int, int, int, int, int, int, int>(Zk_OnAttTransactionEx);
                    });

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
            _logger.LogInformation("Real-time monitoring enabled via STA thread.");
        }

        public async Task<bool> OpenGateAsync()
        {
            if (!IsSdkAvailable || !IsConnected || _zk == null) return false;

            try
            {
                bool result = await _sta.RunAsync(() => (bool)_zk!.ACUnlock(DevicePort, _config.GateOpenDurationMs));
                
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
            if (!IsSdkAvailable || !IsConnected || _zk == null) return false;

            try
            {
                int iyear = 0, imonth = 0, iday = 0, ihour = 0, iminute = 0, isecond = 0;
                bool result = await _sta.RunAsync(() => (bool)_zk!.GetDeviceTime(DevicePort, ref iyear, ref imonth, ref iday, ref ihour, ref iminute, ref isecond));
                
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
            // Event comes from STA thread
            _logger.LogInformation("Hardware Activity: RawData={RawData}, Valid={Valid}, State={State}", enrollNumber, isInValid == 0, attState);
            
            string cardId = enrollNumber;
            string deviceName = "SATT-MAIN";
            string transactionId = $"TXN-{DateTime.UtcNow.Ticks}";

            if (!string.IsNullOrEmpty(enrollNumber) && enrollNumber.Contains(","))
            {
                var parts = enrollNumber.Split(',');
                if (parts.Length >= 1) cardId = parts[0].Trim();
                if (parts.Length >= 2) deviceName = parts[1].Trim();
                if (parts.Length >= 3) transactionId = parts[2].Trim();
                
                _logger.LogDebug("Parsed Scan: CardId={CardId}, Device={Device}, TxId={TxId}", cardId, deviceName, transactionId);
            }

            var timestamp = new DateTime(year, month, day, hour, minute, second);
            
            // iAttState (attState): 0 = Check-In (entering), 1 = Check-Out (leaving)
            var direction = attState == 0
                ? Management.Domain.Enums.ScanDirection.Enter
                : attState == 1
                    ? Management.Domain.Enums.ScanDirection.Exit
                    : Management.Domain.Enums.ScanDirection.Unknown;

            var args = new TurnstileScanEventArgs(cardId, deviceName, transactionId, isInValid == 0, verifyMethod, timestamp, direction);
            
            // Invoke event - note: handler might need to marshal to UI thread if it updates UI
            CardScanned?.Invoke(this, args);
        }

        public async Task DisconnectAsync()
        {
            if (_zk != null && IsConnected)
            {
                await _sta.RunAsync(() =>
                {
                    try { _zk!.Disconnect(); } catch { }
                });
                IsConnected = false;
                _hardwareService.NotifyStatusChanged("Turnstile", false);
                ConnectionStatusChanged?.Invoke(false);
                _logger.LogInformation("Disconnected from ZKTeco device.");
            }
        }

        public void ResetSdkCache()
        {
            _sdkAvailable = null;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            
            if (_zk != null)
            {
                _sta.RunAsync(() =>
                {
                    try { _zk!.OnAttTransactionEx -= new Action<string, int, int, int, int, int, int, int, int, int, int>(Zk_OnAttTransactionEx); } catch { }
                    try { _zk!.Disconnect(); } catch { }
                    _zk = null;
                }).Wait(2000); // Allow graceful cleanup
            }

            _sta.Dispose();
            _isDisposed = true;
        }
    }
}
