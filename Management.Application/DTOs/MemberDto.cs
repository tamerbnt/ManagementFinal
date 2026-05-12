using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Management.Domain.Enums;
using Management.Application.DTOs;

namespace Management.Application.DTOs
{
    public partial class MemberDto : ObservableObject
    {
        [ObservableProperty]
        private Guid _id;

        [ObservableProperty]
        private string _fullName = string.Empty;

        [ObservableProperty]
        private string _email = string.Empty;

        [ObservableProperty]
        private string _phoneNumber = string.Empty;

        [ObservableProperty]
        private string _cardId = string.Empty;

        [ObservableProperty]
        private string _profileImageUrl = string.Empty;

        private MemberStatus _status;
        public MemberStatus Status
        {
            get
            {
                if (_status == MemberStatus.Active && ExpirationDate != default && ExpirationDate <= DateTime.UtcNow)
                {
                    return MemberStatus.Expired;
                }
                return _status;
            }
            set => SetProperty(ref _status, value);
        }


        [ObservableProperty]
        private DateTime _startDate;

        [ObservableProperty]
        private DateTime _expirationDate;

        [ObservableProperty]
        private string _membershipPlanName = string.Empty;

        [ObservableProperty]
        private Guid? _membershipPlanId;

        [ObservableProperty]
        private string _emergencyContactName = string.Empty;

        [ObservableProperty]
        private string _emergencyContactPhone = string.Empty;

        [ObservableProperty]
        private string _notes = string.Empty;
        
        [ObservableProperty]
        private decimal _balance;

        [ObservableProperty]
        private DateTime? _lastVisitDate;

        [ObservableProperty]
        private Gender? _gender;

        [ObservableProperty]
        private DateTime? _dateOfBirth;

        [ObservableProperty]
        private string? _source;

        [ObservableProperty]
        private DateTime _joinedDate = DateTime.Now.AddYears(-1);

        [ObservableProperty]
        private Guid? _manualDiscountId;

        [ObservableProperty]
        private decimal? _manualDiscountAmount;

        [ObservableProperty]
        private bool _isSelected;

        [ObservableProperty]
        private bool _isActive;

        [ObservableProperty]
        private bool _isDeleted;

        [ObservableProperty]
        private int _visitCount;

        [ObservableProperty]
        private System.Collections.Generic.List<AccessEventDto> _accessEvents = new();

        public int DaysRemaining
        {
            get
            {
                if (ExpirationDate == default) return 0;
                var remaining = (ExpirationDate - DateTime.UtcNow).TotalDays;
                return remaining > 0 ? (int)Math.Ceiling(remaining) : 0;
            }
        }

        /// <summary>True when this record is a walk-in lead (not yet a full member).</summary>
        public bool IsLead => _status == MemberStatus.Lead;

        /// <summary>True when membership is frozen — drives the snowflake icon in the UI.</summary>
        public bool IsFrozen => _status == MemberStatus.Frozen;
    }
}

