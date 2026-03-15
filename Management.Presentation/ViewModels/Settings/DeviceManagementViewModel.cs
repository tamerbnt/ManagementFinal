using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Infrastructure.Integrations.Supabase.Models;
using Management.Infrastructure.Services;
using Management.Application.Interfaces.App;
using Management.Domain.Services;
using System.Linq;
using IDialogService = Management.Domain.Services.IDialogService; // Explicitly use Domain service
using Management.Presentation.Extensions;
using Management.Application.Interfaces;
using Management.Application.Interfaces.ViewModels;
using Management.Presentation.Helpers;

namespace Management.Presentation.ViewModels.Settings
{
    public partial class DeviceManagementViewModel : ViewModelBase, INavigationalLifecycle
    {
        private readonly IOnboardingService _onboardingService;
        private readonly ITenantService _tenantService;
        private readonly IHardwareService _hardwareService;
        private readonly IDialogService _dialogService;
        private readonly IToastService _toastService;
        
        public event EventHandler? DevicesChanged;

        [ObservableProperty]
        private bool _isBusy;

        public ObservableRangeCollection<DeviceItemViewModel> Devices { get; } = new();

        public DeviceManagementViewModel(
            IOnboardingService onboardingService,
            ITenantService tenantService,
            IHardwareService hardwareService,
            IDialogService dialogService,
            IToastService toastService)
        {
            _onboardingService = onboardingService;
            _tenantService = tenantService;
            _hardwareService = hardwareService;
            _dialogService = dialogService;
            _toastService = toastService;
            
            Title = "Device Management";
        }
        
        public Task InitializeAsync() => Task.CompletedTask;
        public Task PreInitializeAsync() => Task.CompletedTask;
        public async Task LoadDeferredAsync() => await LoadDevicesAsync();

        public async Task LoadDevicesAsync()
        {
            var tenantId = _tenantService.GetTenantId();
            if (!tenantId.HasValue) return;

            IsBusy = true;
            try
            {
                await Task.Run(async () =>
                {
                    var result = await _onboardingService.GetDevicesAsync(tenantId.Value);
                    if (result.IsSuccess)
                    {
                        var currentHardwareId = _hardwareService.GetHardwareId();
                        var deviceViewModels = result.Value
                            .OrderByDescending(d => d.RegisteredAt)
                            .Select(device => new DeviceItemViewModel(device, device.HardwareId == currentHardwareId, RevokeDeviceCommand))
                            .ToList();

                        System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                        {
                            Devices.ReplaceRange(deviceViewModels);
                        });
                    }
                });
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RevokeDevice(DeviceItemViewModel deviceVm)
        {
            if (deviceVm.IsCurrentDevice)
            {
                await _dialogService.ShowAlertAsync("Access Denied", "You cannot revoke the device you are currently using.");
                return;
            }

            var confirmed = await _dialogService.ShowConfirmationAsync(
                "Revoke License Slot?",
                $"Are you sure you want to revoke '{deviceVm.Label}'? This machine will no longer be able to access the system until reactivated.");

            if (!confirmed) return;

            IsBusy = true;
            try
            {
                var result = await _onboardingService.RevokeDeviceAsync(deviceVm.Id);
                if (result.IsSuccess)
                {
                    Devices.Remove(deviceVm);
                    DevicesChanged?.Invoke(this, EventArgs.Empty);
                    _toastService.ShowSuccess("Revoked Successful", "The license slot is now available for a new machine.");
                }
                else
                {
                    await _dialogService.ShowAlertAsync("Error", result.Error.Message);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        [RelayCommand]
        private async Task RegisterSdk()
        {
            IsBusy = true;
            try
            {
                var app = System.Windows.Application.Current as App;
                if (app == null) return;

                bool success = await Task.Run(() => app.TryRegisterZKTecoSdk(silent: false));
                
                if (success)
                {
                    _toastService.ShowSuccess("SDK Registered", "ZKTeco SDK has been registered successfully. Please restart the application for changes to take effect.");
                }
                else
                {
                    await _dialogService.ShowAlertAsync("Registration Failed", "Failed to register the ZKTeco SDK. Ensure the SDK drivers are present in the app folder and you accepted the administrative prompt.");
                }
            }
            finally
            {
                IsBusy = false;
            }
        }
    }

    public class DeviceItemViewModel : ObservableObject
    {
        private readonly SupabaseDevice _device;
        public Guid Id => _device.Id;
        public string Label => _device.Label ?? "Unnamed PC";
        public string HardwareId => _device.HardwareId;
        public DateTime RegisteredAt => _device.RegisteredAt;
        public bool IsCurrentDevice { get; }
        public IRelayCommand<DeviceItemViewModel> RevokeCommand { get; }

        public DeviceItemViewModel(SupabaseDevice device, bool isCurrent, IRelayCommand<DeviceItemViewModel> revokeCommand)
        {
            _device = device;
            IsCurrentDevice = isCurrent;
            RevokeCommand = revokeCommand;
        }
    }
}
