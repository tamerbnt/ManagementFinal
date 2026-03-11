using System;
using System.IO.Ports; // Requires NuGet: System.IO.Ports
using System.Text;
using Management.Domain.Services;
using Management.Application.Interfaces;
using Management.Infrastructure.Services;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace Management.Infrastructure.Hardware
{
    /// <summary>
    /// Hardware Driver for Serial/COM-based RFID Readers.
    /// Handles buffer accumulation and event firing on background threads.
    /// </summary>
    public class RfidReaderDevice : IRfidReader, IDisposable
    {
        private SerialPort? _serialPort;
        private readonly string _portName;
        private readonly int _baudRate;
        private readonly IHardwareService? _hardwareService;
        private readonly Microsoft.Extensions.Logging.ILogger<RfidReaderDevice>? _logger;

        // Buffer to hold incoming fragments until a newline is found
        private StringBuilder _buffer = new StringBuilder();
        private System.Timers.Timer? _heartbeatTimer;
        private bool _isReconnecting;

        public event Action<string>? CardScanned;
        public event Action<bool>? ConnectionStatusChanged;

        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        /// <summary>
        /// Initializes the driver.
        /// </summary>
        /// <param name="portName">COM Port (e.g. "COM3" on Windows, "/dev/ttyUSB0" on Linux)</param>
        /// <param name="baudRate">Speed (e.g. 9600, 115200) - check hardware manual</param>
        public RfidReaderDevice(
            IHardwareService? hardwareService = null, 
            Microsoft.Extensions.Logging.ILogger<RfidReaderDevice>? logger = null,
            string portName = "COM3", 
            int baudRate = 9600)
        {
            _hardwareService = hardwareService;
            _logger = logger;
            _portName = portName;
            _baudRate = baudRate;
        }

        public void Start()
        {
            if (IsConnected) return;

            try
            {
                _serialPort = new SerialPort(_portName, _baudRate, Parity.None, 8, StopBits.One);
                _serialPort.Handshake = Handshake.None;
                _serialPort.ReadTimeout = 500;
                _serialPort.WriteTimeout = 500;

                // Subscribe to data arrival
                _serialPort.DataReceived += OnDataReceived;

                _serialPort.Open();
                
                // Notify successful connection
                ConnectionStatusChanged?.Invoke(true);
                (_hardwareService as HardwareService)?.NotifyStatusChanged("RFID", true);

                StartHeartbeat();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to open RFID reader on {Port}", _portName);
                (_hardwareService as HardwareService)?.NotifyStatusChanged("RFID", false, ex.Message);
                StartAutoReconnect();
            }
        }

        private void StartHeartbeat()
        {
            _heartbeatTimer?.Stop();
            _heartbeatTimer?.Dispose();

            _heartbeatTimer = new System.Timers.Timer(5000);
            _heartbeatTimer.Elapsed += (s, e) => CheckConnection();
            _heartbeatTimer.AutoReset = true;
            _heartbeatTimer.Start();
        }

        private void CheckConnection()
        {
            if (_serialPort == null || !_serialPort.IsOpen)
            {
                _logger?.LogWarning("RFID Heartbeat: Connection lost.");
                HandleDisconnection("Connection lost");
                return;
            }

            try
            {
                // Accessing a property like DsrHolding or CtsHolding pings the driver
                bool isAlive = _serialPort.CtsHolding || true; 
            }
            catch (Exception ex)
            {
                _logger?.LogWarning("RFID Heartbeat check failed: {Message}", ex.Message);
                HandleDisconnection(ex.Message);
            }
        }

        private void HandleDisconnection(string reason)
        {
            ConnectionStatusChanged?.Invoke(false);
            (_hardwareService as HardwareService)?.NotifyStatusChanged("RFID", false, reason);
            StartAutoReconnect();
        }

        private async void StartAutoReconnect()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            _logger?.LogInformation("RFID: Starting auto-reconnect loop...");
            
            while (!IsConnected && _isReconnecting)
            {
                try
                {
                    await Task.Delay(10000); // Wait 10s between retries
                    Start();
                }
                catch { /* Ignore and retry */ }
            }

            _isReconnecting = false;
        }

        public void Stop()
        {
            _heartbeatTimer?.Stop();
            _isReconnecting = false;

            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.DataReceived -= OnDataReceived;
                    _serialPort.Close();
                }
                _serialPort.Dispose();
                _serialPort = null;
                
                // Notify disconnection
                ConnectionStatusChanged?.Invoke(false);
                (_hardwareService as HardwareService)?.NotifyStatusChanged("RFID", false);
            }
        }

        /// <summary>
        /// Handles raw bytes coming from the hardware.
        /// Reconstructs the Card ID string.
        /// </summary>
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;

            try
            {
                // Read available data
                string data = _serialPort.ReadExisting();

                foreach (char c in data)
                {
                    // Standard RFID readers end transmission with Enter (\r) or Newline (\n)
                    if (c == '\r' || c == '\n')
                    {
                        if (_buffer.Length > 0)
                        {
                            string scanCode = _buffer.ToString();
                            _buffer.Clear();

                            // Fire event (Note: This runs on a background thread)
                            CardScanned?.Invoke(scanCode);
                        }
                    }
                    else
                    {
                        // Append valid character
                        _buffer.Append(c);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Hardware Error] Read failure: {ex.Message}");
            }
        }

        public void SimulateScan(string cardId)
        {
            _logger?.LogInformation("Simulating RFID Scan: {CardId}", cardId);
            CardScanned?.Invoke(cardId);
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
