using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using RelayCommand = CommunityToolkit.Mvvm.Input.RelayCommand;
using Management.Application.Services;
using Management.Domain.Models;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Presentation.ViewModels;

namespace Management.Presentation.Views.Salon;

public record SalonAddStaffArgs(System.Collections.Generic.IEnumerable<Guid> CurrentStaffIds, Action<StaffMember> OnStaffSelected);

public class SalonAddStaffViewModel : ViewModelBase, IInitializable<object>
{
    private readonly IStaffService _staffService;
    private readonly IModalNavigationService _modalService;
    private Action<StaffMember>? _onStaffSelected;
    private bool _isLoading;

    public bool IsLoading
    {
        get => _isLoading;
        set => SetProperty(ref _isLoading, value);
    }

    public ObservableCollection<StaffMember> AvailableStaff { get; } = new();

    private StaffMember? _selectedStaff;
    public StaffMember? SelectedStaff
    {
        get => _selectedStaff;
        set
        {
            if (SetProperty(ref _selectedStaff, value))
            {
                OnPropertyChanged(nameof(CanAddColumn));
            }
        }
    }

    public bool CanAddColumn => SelectedStaff != null;

    public ICommand AddCommand { get; }
    public ICommand CancelCommand { get; }

    public SalonAddStaffViewModel(
        IStaffService staffService, 
        IModalNavigationService modalService)
    {
        _staffService = staffService;
        _modalService = modalService;

        AddCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(ExecuteAdd, () => CanAddColumn);
        CancelCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(() => _modalService.CloseModal());
    }

    public async Task InitializeAsync(object parameter, CancellationToken cancellationToken = default)
    {
        if (parameter is SalonAddStaffArgs args)
        {
            _onStaffSelected = args.OnStaffSelected;
            await LoadAvailableStaff(args.CurrentStaffIds);
        }
    }

    private async Task LoadAvailableStaff(System.Collections.Generic.IEnumerable<Guid> currentStaffIds)
    {
        IsLoading = true;
        try
        {
            var allStaff = await _staffService.GetAllAsync();
            var filtered = allStaff.Where(s => !currentStaffIds.Contains(s.Id)).ToList();
            
            AvailableStaff.Clear();
            foreach (var s in filtered)
            {
                AvailableStaff.Add(s);
            }
        }
        catch (Exception)
        {
            // Error handling could be added here (e.g., via the ModalNavigationService's error event)
        }
        finally
        {
            IsLoading = false;
        }
    }

    private void ExecuteAdd()
    {
        if (SelectedStaff != null)
        {
            _onStaffSelected?.Invoke(SelectedStaff);
            _modalService.CloseModal();
        }
    }
}
