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
        }

        public Guid Id => _appointment.Id;
        public DateTime Timestamp => _appointment.StartTime;
        public DateTime SortDate => Timestamp; // Implement Shared.IActivityItem

        public string Title => _appointment.ClientName ?? _walkInLabel;
        public string Subtitle => _appointment.ServiceName ?? _serviceLabel;
        public string StatusText => _appointment.Status.ToString();
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
        }

        public Guid Id => _sale.Id;
        public DateTime Timestamp => _sale.Timestamp;
        public DateTime SortDate => Timestamp; // Implement Shared.IActivityItem
        
        public string Title => string.IsNullOrEmpty(_sale.MemberName) ? _walkInLabel : _sale.MemberName;
        
        public string Subtitle 
        {
            get 
            {
                if (_itemCount <= 1) return _primaryProduct;
                return _primaryProduct + string.Format(_moreLabel, _itemCount - 1);
            }
        }

        public string StatusText => _saleLabel;
        public string AmountText => $"{_sale.TotalAmount:N2} DA"; 
        public bool IsSale => true;
    }
}
