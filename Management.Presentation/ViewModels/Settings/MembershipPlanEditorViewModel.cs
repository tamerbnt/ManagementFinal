using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Application.DTOs;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Domain.Primitives;
using Management.Domain.Services;
using Management.Presentation.Services;
using Management.Presentation.Extensions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Management.Domain.Models;

namespace Management.Presentation.ViewModels.Settings
{
    public partial class MembershipPlanEditorViewModel : ViewModelBase
    {
        private readonly IMembershipPlanService _planService;
        private readonly IFacilityContextService _facilityContext;
        private readonly ITerminologyService _terminologyService;

        public MembershipPlanEditorViewModel(
            IMembershipPlanService planService,
            IFacilityContextService facilityContext,
            ITerminologyService terminologyService,
            ILogger<MembershipPlanEditorViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService) : base(logger, diagnosticService, toastService)
        {
            _planService = planService;
            _facilityContext = facilityContext;
            _terminologyService = terminologyService;
        }

        [ObservableProperty] private Guid _id;
        [ObservableProperty] private string _name = string.Empty;
        [ObservableProperty] private decimal _price;
        [ObservableProperty] private int _durationDays = 30;
        [ObservableProperty] private bool _isWalkIn;
        [ObservableProperty] private bool _isActive = true;
        [ObservableProperty] private bool _isSessionPack;
        [ObservableProperty] private int _genderRule;
        [ObservableProperty] private bool _isEditMode;

        public ObservableCollection<ScheduleWindowViewModel> ScheduleWindows { get; } = new();

        public event EventHandler? Saved;
        public event EventHandler? Canceled;

        public void Reset()
        {
            Id = Guid.Empty;
            Name = string.Empty;
            Price = 0;
            DurationDays = 30;
            IsWalkIn = false;
            IsActive = true;
            IsSessionPack = false;
            IsEditMode = false;
            GenderRule = 0;
            ScheduleWindows.Clear();
            IsLoading = false;
        }

        public void Initialize(MembershipPlanDto? dto = null)
        {
            Reset();

            if (dto != null)
            {
                Id = dto.Id;
                Name = dto.Name;
                Price = dto.Price;
                DurationDays = dto.DurationDays;
                IsWalkIn = dto.IsWalkIn;
                IsActive = dto.IsActive;
                IsSessionPack = dto.IsSessionPack;
                GenderRule = dto.GenderRule;
                IsEditMode = true;

                if (!string.IsNullOrEmpty(dto.ScheduleJson))
                {
                    try
                    {
                        var windows = JsonSerializer.Deserialize<List<ScheduleWindow>>(dto.ScheduleJson);
                        if (windows != null)
                        {
                            foreach (var w in windows)
                            {
                                ScheduleWindows.Add(new ScheduleWindowViewModel
                                {
                                    DayOfWeek = w.DayOfWeek,
                                    StartTime = w.StartTime.ToString(@"hh\:mm"),
                                    EndTime = w.EndTime.ToString(@"hh\:mm")
                                });
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Failed to deserialize schedule for plan {Id}", Id);
                    }
                }
            }
        }

        [RelayCommand]
        private void AddWindow()
        {
            ScheduleWindows.Add(new ScheduleWindowViewModel());
        }

        [RelayCommand]
        private void RemoveWindow(ScheduleWindowViewModel? window)
        {
            if (window != null)
            {
                ScheduleWindows.Remove(window);
            }
        }

        [RelayCommand]
        public async Task Save()
        {
            await ExecuteLoadingAsync(async () =>
            {
                // Serialize Schedule Windows
                string? scheduleJson = null;
                if (ScheduleWindows.Any())
                {
                    var windows = ScheduleWindows.Select(w => new ScheduleWindow
                    {
                        DayOfWeek = w.DayOfWeek,
                        StartTime = TimeSpan.TryParse(w.StartTime, out var st) ? st : TimeSpan.FromHours(9),
                        EndTime = TimeSpan.TryParse(w.EndTime, out var et) ? et : TimeSpan.FromHours(21),
                        RuleType = 0
                    }).ToList();
                    scheduleJson = JsonSerializer.Serialize(windows);
                }

                var dto = new MembershipPlanDto
                {
                    Id = Id,
                    Name = Name,
                    Price = Price,
                    DurationDays = DurationDays,
                    IsWalkIn = IsWalkIn,
                    IsActive = IsActive,
                    IsSessionPack = IsSessionPack,
                    GenderRule = GenderRule,
                    ScheduleJson = scheduleJson
                };

                Result result;
                if (!IsEditMode)
                {
                    result = await _planService.CreatePlanAsync(_facilityContext.CurrentFacilityId, dto);
                }
                else
                {
                    result = await _planService.UpdatePlanAsync(_facilityContext.CurrentFacilityId, dto);
                }

                if (result.IsSuccess)
                {
                    _toastService?.ShowSuccess(_terminologyService.GetTerm("Terminology.Settings.Editor.SaveSuccess"));
                    Saved?.Invoke(this, EventArgs.Empty);
                }
                else
                {
                    _toastService?.ShowError(result.Error?.Message ?? "Error saving plan");
                }
            }, _terminologyService.GetTerm("Terminology.Settings.Editor.Saving"));
        }

        [RelayCommand]
        public void Cancel()
        {
            Canceled?.Invoke(this, EventArgs.Empty);
        }
    }
}
