using System;
using System.IO.Ports; // Requires NuGet: System.IO.Ports
using System.Text;
using Management.Domain.Services;

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

        // Buffer to hold incoming fragments until a newline is found
        private StringBuilder _buffer = new StringBuilder();

        public event Action<string>? CardScanned;

        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        /// <summary>
        /// Initializes the driver.
        /// </summary>
        /// <param name="portName">COM Port (e.g. "COM3" on Windows, "/dev/ttyUSB0" on Linux)</param>
        /// <param name="baudRate">Speed (e.g. 9600, 115200) - check hardware manual</param>
        public RfidReaderDevice(string portName = "COM3", int baudRate = 9600)
        {
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

                // Subscribe to data arrival
                _serialPort.DataReceived += OnDataReceived;

                _serialPort.Open();
            }
            catch (Exception ex)
            {
                // Log failure to connect to hardware
                Console.WriteLine($"[Hardware Error] Failed to open RFID reader on {_portName}: {ex.Message}");
                // In a real app, rethrow or fire an error event
            }
        }

        public void Stop()
        {
            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    _serialPort.DataReceived -= OnDataReceived;
                    _serialPort.Close();
                }
                _serialPort.Dispose();
                _serialPort = null;
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

        public void Dispose()
        {
            Stop();
        }
    }
}