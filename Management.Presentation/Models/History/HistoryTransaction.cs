using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Management.Presentation.Models.History
{
    public partial class HistoryTransaction : ObservableObject
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public Guid? MemberId { get; set; }
        public TransactionStatus Status { get; set; } = TransactionStatus.Completed;
        
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasAuditNote))]
        private string? _auditNote;

        public bool HasAuditNote => !string.IsNullOrWhiteSpace(AuditNote);
    }
}
