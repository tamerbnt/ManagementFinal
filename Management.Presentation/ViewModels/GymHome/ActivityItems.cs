using System;
using Management.Domain.Models;
using Management.Domain.Enums;

namespace Management.Presentation.ViewModels.GymHome
{
    public interface IActivityLogItem
    {
        DateTime Timestamp { get; }
        string Title { get; }
        string StatusMessage { get; }
        AccessResult ResultStatus { get; }
    }

    public class MemberAccessItem : IActivityLogItem
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public Member? Member { get; set; }
        public string Title => Member?.FullName ?? "Unknown Member";
        public string StatusMessage { get; set; } = string.Empty;
        public AccessResult ResultStatus { get; set; }
        public string? PhotoPath => Member?.ProfileImageUrl;
        public int DaysRemaining { get; set; }
    }

    public class WalkInItem : IActivityLogItem
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public string Title => "Walk-In Guest";
        public string StatusMessage => $"Entry logged - {Amount:C}";
        public AccessResult ResultStatus => AccessResult.Granted;
        public decimal Amount { get; set; }
        public string ReceiptNumber { get; set; } = string.Empty;
    }

    public class SaleItem : IActivityLogItem
    {
        public DateTime Timestamp { get; } = DateTime.Now;
        public string Title => $"Sale: {ProductName}";
        public string StatusMessage => $"Collected {Amount:C}";
        public AccessResult ResultStatus => AccessResult.Granted;
        public string ProductName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? MemberName { get; set; }
    }
}
