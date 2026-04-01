using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Management.Domain.Enums;

namespace Management.Presentation.ViewModels.Auth
{
    /// <summary>
    /// Represents a facility choice in the onboarding/login flow.
    /// </summary>
    public class FacilityTypeOption : ObservableObject
    {
        public Guid Id { get; set; }
        public FacilityType Type { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconKey { get; set; } = icon_gym; // Default
        public string GradientStart { get; set; } = "#0EA5E9";
        public string GradientEnd { get; set; } = "#2563EB";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }

        // Icon path constants for the UI (Material Icons-like paths)
        public const string icon_gym = "M12,2A10,10,0,1,0,22,12,10,10,0,0,0,12,2Zm5,11H13v4H11V13H7V11h4V7h2v4h4Z";
        public const string icon_salon = "M12,20A8,8,0,1,1,20,12,8,8,0,0,1,12,20ZM12,4A8,8,0,1,0,4,12,8,8,0,0,0,12,4Z";
        public const string icon_restaurant = "M11,9H9V2H7V9H5V2H3V9C3,11.12 4.66,12.84 6.75,12.97V22H9.25V12.97C11.34,12.84 13,11.12 13,9V2H11V9Z M16,6V14H18.5V22H21V2C18.24,2 16,4.24 16,6Z";
    }

    public class OnboardingSlide : ObservableObject
    {
        public string ImagePath { get; set; } = string.Empty;
        public string EmotionalHeadline { get; set; } = string.Empty;
        public string TechnicalSubtitle { get; set; } = string.Empty;
        public string BackgroundColor { get; set; } = "#FFFFFF";
        public string TitleColor { get; set; } = "#FACC15"; // Artistic Yellow
        public string SubtitleColor { get; set; } = "#111827"; // Artistic Black

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set => SetProperty(ref _isSelected, value);
        }
    }
}
