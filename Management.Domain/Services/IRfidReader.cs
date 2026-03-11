using System;

namespace Management.Domain.Services
{
    /// <summary>
    /// Defines the contract for any device (physical or virtual) that captures
    /// RFID card scans.
    /// Implementation resides in Infrastructure.Hardware.
    /// </summary>
    public interface IRfidReader
    {
        /// <summary>
        /// Fired whenever a card ID is successfully parsed from the input stream.
        /// Payload: The raw Card ID string (e.g., "E004015093A").
        /// </summary>
        event Action<string> CardScanned;

        /// <summary>
        /// Indicates if the underlying hardware connection is active and listening.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Fired when the hardware connection status changes (True = Connected, False = Disconnected).
        /// </summary>
        event Action<bool> ConnectionStatusChanged;

        /// <summary>
        /// Opens the connection (Serial Port / HID) and begins listening for data.
        /// Should handle connection errors internally or throw specific hardware exceptions.
        /// </summary>
        void Start();

        /// <summary>
        /// Closes the connection and releases hardware resources.
        /// </summary>
        void Stop();

        /// <summary>
        /// Manually triggers a scan event for testing or simulation.
        /// </summary>
        void SimulateScan(string cardId);
    }
}
