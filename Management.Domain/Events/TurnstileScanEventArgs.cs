using System;

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

        public TurnstileScanEventArgs(
            string cardId,
            string deviceName,
            string transactionId,
            bool isValid,
            int verificationMethod,
            DateTime timestamp)
        {
            CardId = cardId;
            DeviceName = deviceName;
            TransactionId = transactionId;
            IsValid = isValid;
            VerificationMethod = verificationMethod;
            Timestamp = timestamp;
        }
    }
}
