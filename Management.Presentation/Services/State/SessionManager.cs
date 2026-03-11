using Management.Application.DTOs;
using Management.Domain.Enums;
using CommunityToolkit.Mvvm.ComponentModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

using Management.Domain.Interfaces;

namespace Management.Presentation.Services.State
{
    public class SessionManager : ObservableObject, IStateResettable
    {
        public void ResetState()
        {
            // Note: We don't necessarily clear the CurrentUser here because 
            // the user stays the same across facility switches.
            // But we might want to clear facility-specific settings.
            CurrentFacility = FacilityType.General; // Default to general segment
        }
        private readonly object _lock = new object();
        private StaffDto? _currentUser;
        private FacilityType _currentFacility;

        public StaffDto? CurrentUser
        {
            get { lock (_lock) return _currentUser; }
            private set 
            { 
                lock (_lock) 
                {
                    if (SetProperty(ref _currentUser, value))
                    {
                        OnPropertyChanged(nameof(IsLoggedIn));
                        OnPropertyChanged(nameof(UserDisplayInitials));
                        OnPropertyChanged(nameof(CurrentTenantId));
                    }
                } 
            }
        }

        public FacilityType CurrentFacility
        {
            get { lock (_lock) return _currentFacility; }
            set 
            { 
                lock (_lock) SetProperty(ref _currentFacility, value); 
            }
        }

        public bool IsLoggedIn => CurrentUser != null;

        public Guid CurrentTenantId => CurrentUser?.TenantId ?? Guid.Empty;

        public string UserDisplayInitials
        {
            get
            {
                if (CurrentUser == null || string.IsNullOrWhiteSpace(CurrentUser.FullName)) return "??";
                var parts = CurrentUser.FullName.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return "??";
                if (parts.Length == 1) return parts[0][0].ToString().ToUpper();
                return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            }
        }

        public void SetUser(StaffDto user)
        {
            CurrentUser = user;
        }

        public void Clear()
        {
            CurrentUser = null;
        }
    }
}
