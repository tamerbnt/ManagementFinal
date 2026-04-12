using CommunityToolkit.Mvvm.ComponentModel;
using Management.Application.DTOs;
using System;

namespace Management.Presentation.ViewModels.Settings
{
    public partial class PromotionViewModel : ObservableObject
    {
        private readonly PromotionDto _dto;

        public Guid Id => _dto.Id;
        public string Name => _dto.Name;
        public string Description => _dto.Description ?? "No description";
        public string TargetType => _dto.TargetType.ToString();
        public string DiscountType => _dto.IsPercentage ? "Percentage" : "Fixed Amount";
        public decimal DiscountValue => _dto.DiscountValue;
        public string Criteria => GetCriteriaDescription();
        public bool IsActive => _dto.IsActive;

        public PromotionViewModel(PromotionDto dto)
        {
            _dto = dto;
        }

        private string GetCriteriaDescription()
        {
            var gender = _dto.RequiredGender?.ToString() ?? "Any";
            var plan = string.IsNullOrEmpty(_dto.RequiredMembershipPlanName) ? "Any" : _dto.RequiredMembershipPlanName;
            return $"Gender: {gender}, Plan: {plan}";
        }
    }
}
