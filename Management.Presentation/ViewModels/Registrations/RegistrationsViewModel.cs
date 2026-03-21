using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Input;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Presentation.Extensions;
using Microsoft.Extensions.Logging;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Presentation.Services;
using Management.Domain.Services;
using Management.Infrastructure.Services;
using Management.Infrastructure.Integrations.Supabase.Models;
using Management.Presentation.Helpers;
using Management.Application.DTOs;
using Management.Application.Interfaces.ViewModels;

namespace Management.Presentation.ViewModels.Registrations
{
    public partial class RegistrationsViewModel : ViewModelBase, INavigationalLifecycle
    {
        [ObservableProperty]
        private string _terminologyPluralLabel = "Registrations";

        public Guid FacilityId => _facilityContext.CurrentFacilityId;

        [ObservableProperty]
        private string _terminologyLabel = "Registration";

        [ObservableProperty]
        private int _pendingWebsiteCount;

        [ObservableProperty]
        private string _searchText = string.Empty;

        public ObservableCollection<SupabaseRegistrationRequest> WebsiteRequests { get; } = new();

        public IAsyncRelayCommand<SupabaseRegistrationRequest> ConfirmWebsiteRequestCommand { get; }
        public IAsyncRelayCommand<SupabaseRegistrationRequest> RejectWebsiteRequestCommand { get; }

        private readonly IFacilityContextService _facilityContext;
        private readonly IWebsiteRegistrationService _websiteRegistrationService;
        private readonly SupabaseRealtimeService _realtimeService;
        private readonly Management.Domain.Services.IDialogService _dialogService;

        public RegistrationsViewModel(
            ILogger<RegistrationsViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService,
            IFacilityContextService facilityContext,
            IWebsiteRegistrationService websiteRegistrationService,
            SupabaseRealtimeService realtimeService,
            Management.Domain.Services.IDialogService dialogService)
            : base(logger, diagnosticService, toastService)
        {
            _facilityContext = facilityContext;
            _websiteRegistrationService = websiteRegistrationService;
            _realtimeService = realtimeService;
            _dialogService = dialogService;

            ConfirmWebsiteRequestCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<SupabaseRegistrationRequest>(async request => 
            {
                if (request == null) return;

                var prefillData = new Management.Presentation.ViewModels.Members.QuickRegistrationPrefillData(
                    request.FullName,
                    request.Email,
                    request.PhoneNumber,
                    request.Gender.Equals("Female", StringComparison.OrdinalIgnoreCase) ? Management.Domain.Enums.Gender.Female : Management.Domain.Enums.Gender.Male
                );

                var result = await _dialogService.ShowCustomDialogAsync<Management.Presentation.ViewModels.Members.QuickRegistrationViewModel>(prefillData);

                if (result is Management.Presentation.Stores.ModalResult modalResult && modalResult.IsSuccess)
                {
                    WebsiteRequests.Remove(request);
                    PendingWebsiteCount = WebsiteRequests.Count;
                    _toastService.ShowSuccess("Registration confirmed and member created.");
                    _ = _websiteRegistrationService.UpdateRequestStatusAsync(request.Id, "confirmed");
                }
            });

            RejectWebsiteRequestCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand<SupabaseRegistrationRequest>(async request => 
            {
                if (request == null) return;
                
                WebsiteRequests.Remove(request);
                PendingWebsiteCount = WebsiteRequests.Count;
                
                await _websiteRegistrationService.UpdateRequestStatusAsync(request.Id, "rejected");
                _toastService.ShowSuccess($"Rejected request from {request.FullName}");
            });
        }

        public Task PreInitializeAsync()
        {
            Title = "Registrations";
            return Task.CompletedTask;
        }

        public Task InitializeAsync() => Task.CompletedTask;

        public async Task LoadDeferredAsync()
        {
            IsActive = true;
            _realtimeService.OnWebsiteRegistrationRequestReceived -= OnWebsiteRequestReceived;
            _realtimeService.OnWebsiteRegistrationRequestReceived += OnWebsiteRequestReceived;
            
            await ExecuteLoadingAsync(async () =>
            {
                await LoadPendingWebsiteRequestsAsync();
            });
        }

        private async Task LoadPendingWebsiteRequestsAsync()
        {
            if (string.IsNullOrEmpty(_facilityContext.PublicSlug))
                return;

            try
            {
                var requests = await _websiteRegistrationService.FetchPendingRequestsAsync(_facilityContext.PublicSlug);
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
                {
                    WebsiteRequests.Clear();
                    foreach (var request in requests)
                    {
                        WebsiteRequests.Add(request);
                    }
                    PendingWebsiteCount = WebsiteRequests.Count;
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load pending website requests");
            }
        }

        private void OnWebsiteRequestReceived(SupabaseRegistrationRequest request)
        {
            if (request.FacilitySlug != _facilityContext.PublicSlug) return;
            if (request.Status != "pending") return;

            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => 
            {
                // Prevent duplicates
                if (!WebsiteRequests.Any(r => r.Id == request.Id))
                {
                    WebsiteRequests.Insert(0, request);
                    PendingWebsiteCount = WebsiteRequests.Count;
                }
            });
        }

        public void OnNavigatedFrom()
        {
            _realtimeService.OnWebsiteRegistrationRequestReceived -= OnWebsiteRequestReceived;
        }



    }
}
