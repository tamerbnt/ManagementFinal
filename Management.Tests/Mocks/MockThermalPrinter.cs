using System;
using System.Threading.Tasks;

namespace Management.Tests.Mocks
{
    public class MockThermalPrinter
    {
        public bool SimulateTimeout { get; set; }
        public bool SimulateOffline { get; set; }
        public int TimeoutDelayMs { get; set; } = 5000;

        public async Task<bool> PrintAsync(string content)
        {
            if (SimulateOffline)
            {
                throw new InvalidOperationException("Printer is offline");
            }

            if (SimulateTimeout)
            {
                await Task.Delay(TimeoutDelayMs);
                throw new TimeoutException("Printer did not respond within the timeout period");
            }

            // Simulate successful print
            await Task.Delay(100);
            return true;
        }

        public Task<bool> CheckStatusAsync()
        {
            if (SimulateOffline)
            {
                return Task.FromResult(false);
            }

            return Task.FromResult(true);
        }
    }
}
