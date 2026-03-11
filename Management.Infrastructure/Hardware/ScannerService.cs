using System;
using System.Text;
using System.Diagnostics;

namespace Management.Infrastructure.Hardware
{
    /// <summary>
    /// Detects HID Scanner input based on character timing.
    /// Scanners typically send characters very rapidly compared to human typing.
    /// </summary>
    public class ScannerService
    {
        private readonly StringBuilder _buffer = new StringBuilder();
        private readonly Stopwatch _stopwatch = new Stopwatch();
        private const long MaxCharacterDelayMs = 50; // Delay between characters to be considered part of a scan

        public event Action<string>? ScanCompleted;

        public void ProcessKey(string keyText)
        {
            if (string.IsNullOrEmpty(keyText)) return;

            long elapsed = _stopwatch.ElapsedMilliseconds;
            _stopwatch.Restart();

            // If delay is too long, it's likely a new input or human typing
            if (elapsed > MaxCharacterDelayMs && _buffer.Length > 0)
            {
                _buffer.Clear();
            }

            // Enter key usually signals the end of an HID scan
            if (keyText == "\r" || keyText == "\n")
            {
                if (_buffer.Length > 0)
                {
                    string result = _buffer.ToString();
                    _buffer.Clear();
                    ScanCompleted?.Invoke(result);
                }
                return;
            }

            // Append character to buffer
            if (keyText.Length == 1)
            {
                _buffer.Append(keyText);
            }
        }

        public void Clear()
        {
            _buffer.Clear();
            _stopwatch.Reset();
        }
    }
}
