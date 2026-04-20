using System;
using Management.Domain.Models.Salon;
using Management.Application.DTOs;
using Management.Presentation.ViewModels.Shared;

namespace Management.Presentation.ViewModels.Salon
{
    public class AppointmentActivityItem : IActivityItem
    {
        private readonly Appointment _appointment;
        private readonly string _walkInLabel;
        private readonly string _serviceLabel;
        
        public AppointmentActivityItem(Appointment appointment, string walkInLabel, string serviceLabel)
        {
            _appointment = appointment;
            _walkInLabel = walkInLabel;
            _serviceLabel = serviceLabel;
            
            // Parity properties
            Name = _appointment.ClientName ?? _walkInLabel;
            Status = _appointment.Status.ToString();
            AvatarInitials = !string.IsNullOrEmpty(Name) 
                ? new string(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0]).Take(2).ToArray()).ToUpper()
                : "??";
            Icon = "👤";
        }

        public Guid Id => _appointment.Id;
        public DateTime Timestamp => _appointment.StartTime;
        public DateTime SortDate => Timestamp;

        public string Name { get; }
        public string Status { get; }
        public string AvatarInitials { get; }
        public string Icon { get; }

        public string RelativeTime
        {
            get
            {
                var diff = DateTime.Now - SortDate;
                if (diff.TotalMinutes < 1) return "now";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}min ago";
                if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
                return $"{(int)diff.TotalDays}d ago";
            }
        }

        public string Title => Name;
        public string Subtitle => _appointment.ServiceName ?? _serviceLabel;
        public string StatusText => Status;
        public string AmountText => ""; 
        public bool IsSale => false;

        public Appointment Appointment => _appointment;
    }

    public class SaleActivityItem : IActivityItem
    {
        private readonly SaleDto _sale;
        private readonly string _primaryProduct;
        private readonly int _itemCount;
        private readonly string _walkInLabel;
        private readonly string _moreLabel;
        private readonly string _saleLabel;

        public SaleActivityItem(SaleDto sale, string primaryProduct, int itemCount, string walkInLabel, string moreLabel, string saleLabel)
        {
            _sale = sale;
            _primaryProduct = primaryProduct;
            _itemCount = itemCount;
            _walkInLabel = walkInLabel;
            _moreLabel = moreLabel;
            _saleLabel = saleLabel;

            // Parity properties
            Name = string.IsNullOrEmpty(_sale.MemberName) ? _walkInLabel : _sale.MemberName;
            Status = _saleLabel;
            AvatarInitials = !string.IsNullOrEmpty(Name) && Name != _walkInLabel
                ? new string(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(s => s[0]).Take(2).ToArray()).ToUpper()
                : "$$";
            Icon = "🛒";
        }

        public Guid Id => _sale.Id;
        public DateTime Timestamp => _sale.Timestamp;
        public DateTime SortDate => Timestamp;

        public string Name { get; }
        public string Status { get; }
        public string AvatarInitials { get; }
        public string Icon { get; }

        public string RelativeTime
        {
            get
            {
                var diff = DateTime.Now - SortDate;
                if (diff.TotalMinutes < 1) return "now";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes}min ago";
                if (diff.TotalDays < 1) return $"{(int)diff.TotalHours}h ago";
                return $"{(int)diff.TotalDays}d ago";
            }
        }
        
        public string Title => Name;
        
        public string Subtitle 
        {
            get 
            {
                if (_itemCount <= 1) return _primaryProduct;
                return _primaryProduct + string.Format(_moreLabel, _itemCount - 1);
            }
        }

        public string StatusText => Status;
        public string AmountText => $"{_sale.TotalAmount:N2} DA"; 
        public bool IsSale => true;
    }
}
