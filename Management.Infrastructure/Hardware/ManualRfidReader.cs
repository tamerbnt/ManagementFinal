using System;
using Management.Domain.Services;

namespace Management.Infrastructure.Hardware
{
    /// <summary>
    /// A software-only RFID reader implementation.
    /// Used for:
    /// 1. Development/Testing without physical hardware.
    /// 2. Manual reception desk entry (typing a card number).
    /// </summary>
    public class ManualRfidReader : IRfidReader
    {
        public event Action<string>? CardScanned;

        public bool IsConnected { get; private set; }

        public void Start()
        {
            // Virtual device is always ready
            IsConnected = true;
        }

        public void Stop()
        {
            IsConnected = false;
        }

        /// <summary>
        /// Manually triggers a scan event. 
        /// This method is specific to the Manual implementation and is called 
        /// by the AccessControlViewModel commands.
        /// </summary>
        /// <param name="cardId">The Card ID to simulate (e.g., from a text box or random generator).</param>
        public void SimulateScan(string cardId)
        {
            if (!IsConnected) return;

            // Fire the event as if hardware just sent data
            CardScanned?.Invoke(cardId);
        }
    }
}