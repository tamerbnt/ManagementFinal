using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Management.Infrastructure.Configuration;

namespace Management.Infrastructure.Hardware
{
    /// <summary>
    /// Network Driver for IP-based Turnstile Relays / PLCs.
    /// Handles the low-level TCP/IP socket communication to trigger physical unlocking.
    /// </summary>
    public class TurnstileController
    {
        // Standard timeout to prevent UI freezes if hardware is offline
        private const int ConnectionTimeoutMs = 500;

        // Command protocol (varies by hardware manufacturer)
        // Example: Sending "RELAY1_ON" string, or specific hex bytes
        private const string UnlockCommand = "RELAY_TRIGGER_1";

        /// <summary>
        /// Sends a signal to the physical hardware to unlock the gate.
        /// </summary>
        /// <param name="ipAddress">Device IP (e.g. 192.168.1.50)</param>
        /// <param name="port">Device Port (usually 80, 23, or 502 for Modbus)</param>
        /// <returns>True if signal sent successfully, False if device unreachable.</returns>
        public async Task<bool> SendUnlockSignalAsync(string ipAddress, int port = 8080)
        {
            try
            {
                using (var client = new TcpClient())
                {
                // 1. Connect with aggressive timeout and retry
                await ResiliencePolicyRegistry.HardwareRetryPolicy.ExecuteAsync(async () =>
                {
                    var connectTask = client.ConnectAsync(ipAddress, port);
                    var timeoutTask = Task.Delay(TimeSpan.FromMilliseconds(500));

                    var completedTask = await Task.WhenAny(connectTask, timeoutTask);
                    if (completedTask == timeoutTask) throw new TimeoutException("Hardware connection timed out");

                    await connectTask;
                });

                if (!client.Connected) return false;

                // 2. Send Trigger Command
                using (var stream = client.GetStream())
                {
                    byte[] data = Encoding.ASCII.GetBytes(UnlockCommand);
                    await stream.WriteAsync(data, 0, data.Length);
                }
                }

                return true;
            }
            catch (Exception)
            {
                // Log hardware failure (e.g. "Connection Refused")
                // In production: _logger.LogError($"Turnstile at {ipAddress} failed: {ex.Message}");
                return false;
            }
        }
    }
}