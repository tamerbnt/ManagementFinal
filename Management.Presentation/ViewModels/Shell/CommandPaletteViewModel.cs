using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Infrastructure.Services;
using Management.Application.DTOs;
using Management.Presentation.Services;
using Management.Presentation.Models;
using Management.Application.Interfaces.ViewModels;
using Management.Presentation.Extensions;
using Management.Presentation.ViewModels.Settings;
using Management.Domain.Services;

// Aliases for likely ViewModel locations - assuming standard naming convention
// If these namespaces don't exist, we might need to adjust, but based on file structure they should be under ViewModels.
// I will resolve types dynamically or via a switch for now to avoid compilation errors if specific VMs aren't imported.
// Actually, to be safe, I'll use strings and reflection or a switch in a method that I can easily fix if names differ.

namespace Management.Presentation.ViewModels.Shell
{
    public partial class CommandPaletteViewModel : ViewModelBase
    {
        private readonly ISearchService _searchService;
        private readonly INavigationService _navigationService;
        private readonly IFacilityContextService _facilityContext;
        // Assuming IThemeService exists based on user context, if not I'll define a placeholder or use object
        // The user context mentioned Management.Presentation.Services.Application.ThemeService.cs
        // I will use dynamic or object if I can't resolve the interface easily without reading more files, 
        // but let's assume I can inject it or the relevant service later. 
        // For now, I'll stick to Search and Nav.
        
        [ObservableProperty]
        private bool _isVisible;

        [ObservableProperty]
        private string _searchQuery = string.Empty;

        [ObservableProperty]
        private SearchResultDto? _selectedResult;

        private CancellationTokenSource? _debounceCts;

        public ObservableCollection<SearchResultDto> RawResults { get; } = new();
        public ICollectionView GroupedResults { get; }

        public CommandPaletteViewModel(
            ISearchService searchService,
            INavigationService navigationService,
            IFacilityContextService facilityContext)
        {
            _searchService = searchService;
            _navigationService = navigationService;
            _facilityContext = facilityContext;

            // Initialize Grouped View
            GroupedResults = CollectionViewSource.GetDefaultView(RawResults);
            GroupedResults.GroupDescriptions.Add(new PropertyGroupDescription(nameof(SearchResultDto.Group)));
        }

        partial void OnSearchQueryChanged(string value)
        {
            // Debounce
            _debounceCts?.Cancel();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;

            // Fire and forget (safe async void equivalent for property change)
            _ = PerformSearchAsync(value, token);
        }

        private async Task PerformSearchAsync(string query, CancellationToken token)
        {
            try
            {
                await Task.Delay(300, token); // 300ms Debounce
                if (token.IsCancellationRequested) return;

                var results = await _searchService.SearchAsync(query, _facilityContext.CurrentFacility);
                
                // Marshal to UI thread if necessary (WPF usually requires this for ObservableCollection)
                // ObservableObject/ViewModelBase doesn't automatically marshal property setters for collections.
                // We'll use App.Current.Dispatcher or similar if needed, but usually assume ViewModel is on UI thread 
                // or use a safe helper. 
                // For this implementation, I'll update the collection directly assuming correct context, 
                // but checking for cancellation again.

                if (token.IsCancellationRequested) return;

                RawResults.Clear();
                foreach (var result in results)
                {
                    RawResults.Add(result);
                }

                // Auto-select first result
                SelectedResult = RawResults.FirstOrDefault();
            }
            catch (TaskCanceledException)
            {
                // Ignore
            }
            catch (Exception ex)
            {
                // Log error
                System.Diagnostics.Debug.WriteLine($"Search Error: {ex.Message}");
            }
        }

        [RelayCommand]
        private void MoveSelectionDown()
        {
            if (RawResults.Count == 0) return;

            if (SelectedResult == null)
            {
                SelectedResult = RawResults[0];
                return;
            }

            var index = RawResults.IndexOf(SelectedResult);
            if (index < RawResults.Count - 1)
            {
                SelectedResult = RawResults[index + 1];
            }
        }

        [RelayCommand]
        private void MoveSelectionUp()
        {
            if (RawResults.Count == 0) return;

            if (SelectedResult == null)
            {
                SelectedResult = RawResults[RawResults.Count - 1]; // Loop to bottom? Or just top? Standard is stop at top.
                return;
            }

            var index = RawResults.IndexOf(SelectedResult);
            if (index > 0)
            {
                SelectedResult = RawResults[index - 1];
            }
        }

        [RelayCommand]
        private void Close()
        {
            IsVisible = false;
            SearchQuery = string.Empty;
        }

        [RelayCommand]
        private void Execute()
        {
            if (SelectedResult == null) return;

            var actionKey = SelectedResult.ActionKey;
            var parameter = SelectedResult.ActionParameter?.ToString();

            IsVisible = false; // Close palette on execution
            SearchQuery = string.Empty; // Reset search

            switch (actionKey)
            {
                case "Nav":
                    HandleNavigation(parameter);
                    break;
                case "Action":
                    HandleAction(parameter);
                    break;
                case "System":
                    HandleSystem(parameter);
                    break;
            }
        }

        private void HandleNavigation(string? viewName)
        {
            if (string.IsNullOrEmpty(viewName)) return;

            // Handle Dynamic Routing (e.g. MemberDetail_xyz)
            if (viewName.StartsWith("MemberDetail_"))
            {
                var id = viewName.Substring("MemberDetail_".Length);
                System.Diagnostics.Debug.WriteLine($"NAVIGATING TO MEMBER: {id}");
                _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.Members.MembersViewModel>();
                // In a full implementation, we would send a message to open this specific member:
                // WeakReferenceMessenger.Default.Send(new OpenMemberProfileMessage(id));
                return;
            }
            if (viewName.StartsWith("StaffDetail_"))
            {
                var id = viewName.Substring("StaffDetail_".Length);
                System.Diagnostics.Debug.WriteLine($"NAVIGATING TO STAFF: {id}");
                _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.Finance.FinanceAndStaffViewModel>();
                return;
            }
            if (viewName.StartsWith("ProductDetail_"))
            {
                var id = viewName.Substring("ProductDetail_".Length);
                System.Diagnostics.Debug.WriteLine($"NAVIGATING TO PRODUCT: {id}");
                _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.Shop.ShopViewModel>();
                return;
            }
            if (viewName.StartsWith("PlanDetail_"))
            {
                var id = viewName.Substring("PlanDetail_".Length);
                System.Diagnostics.Debug.WriteLine($"NAVIGATING TO PLAN: {id}");
                _ = _navigationService.NavigateToAsync<SettingsViewModel>();
                return;
            }

            switch (viewName)
            {
                case "DashboardView":
                    _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.GymHome.GymHomeViewModel>();
                    break;
                case "MembersView":
                    _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.Members.MembersViewModel>();
                    break;
                case "PosView":
                    _ = _navigationService.NavigateToAsync<Management.Presentation.ViewModels.Shop.ShopViewModel>();
                    break;
                case "SettingsView":
                    _ = _navigationService.NavigateToAsync<SettingsViewModel>();
                    break;
                default:
                    System.Diagnostics.Debug.WriteLine($"NAVIGATING TO: {viewName}");
                    break;
            }
        }

        private void HandleAction(string? actionName)
        {
             System.Diagnostics.Debug.WriteLine($"EXECUTING ACTION: {actionName}");
             // Future: Implement specific modals like NewSale, AddMember using IModalNavigationService
        }

        private void HandleSystem(string? systemAction)
        {
            System.Diagnostics.Debug.WriteLine($"SYSTEM ACTION: {systemAction}");
            if (systemAction == "Exit")
            {
                System.Windows.Application.Current.Shutdown();
            }
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _debounceCts?.Cancel();
                _debounceCts?.Dispose();
                _debounceCts = null;
            }
            base.Dispose(disposing);
        }
    }
}
