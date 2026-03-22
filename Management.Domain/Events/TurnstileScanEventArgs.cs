using System;
using Management.Domain.Enums;

namespace Management.Domain.Events
{
    public class TurnstileScanEventArgs : EventArgs
    {
        public string CardId { get; }
        public string DeviceName { get; }
        public string TransactionId { get; }
        public bool IsValid { get; }
        public int VerificationMethod { get; }
        public DateTime Timestamp { get; }
        /// <summary>
        /// Scan direction from ZKTeco iAttState parameter.
        /// 0 = Check-In (Enter), 1 = Check-Out (Exit)
        /// </summary>
        public ScanDirection Direction { get; }

        public TurnstileScanEventArgs(
            string cardId,
            string deviceName,
            string transactionId,
            bool isValid,
            int verificationMethod,
            DateTime timestamp,
            ScanDirection direction = ScanDirection.Enter)
        {
            CardId = cardId;
            DeviceName = deviceName;
            TransactionId = transactionId;
            IsValid = isValid;
            VerificationMethod = verificationMethod;
            Timestamp = timestamp;
            Direction = direction;
        }
    }
}
