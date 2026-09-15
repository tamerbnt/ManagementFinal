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
        public bool IsAvailable { get; set; } = true;
        public string BadgeText { get; set; } = string.Empty;

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

        // ── Phase 2 Six Archetype Icons ───────────────────────────────────
        // POS & Order/Inventory — shopping cart / barcode
        public const string icon_pos = "M17,18C15.89,18 15,18.89 15,20A2,2 0 0,0 17,22A2,2 0 0,0 19,20C19,18.89 18.1,18 17,18M1,2V4H3L6.6,11.59L5.24,14.04C5.09,14.32 5,14.65 5,15A2,2 0 0,0 7,17H19V15H7.42A0.25,0.25 0 0,1 7.17,14.75C7.17,14.7 7.18,14.66 7.2,14.63L8.1,13H15.55C16.3,13 16.96,12.58 17.3,11.97L20.88,5.5C20.95,5.34 21,5.17 21,5A1,1 0 0,0 20,4H5.21L4.27,2M7,18C5.89,18 5,18.89 5,20A2,2 0 0,0 7,22A2,2 0 0,0 9,20C9,18.89 8.1,18 7,18Z";
        // Appointment & Service — scissors / sparkle
        public const string icon_appointment = "M13.78,15.3L19.78,21.3L21.89,19.14L15.89,13.14M17.5,11.5C17.97,11.5 18.44,11.44 18.88,11.32L6.44,23.76L4.04,21.36L10.29,15.12L8.5,13.31L6.09,15.72L4.68,14.31L1.5,17.5L6.5,22.5L21.5,7.5L17.5,3.5C15.14,3.5 13.13,4.86 12.18,6.84L12.15,6.81L6.5,12.46L8.97,14.96L12.68,11.25C13.5,11.4 14.28,11.5 15.5,11.5H17.5Z";
        // Membership & Session — dumbbell / shield
        public const string icon_membership = "M20.57,14.86L22,13.43L20.57,12L17,15.57L8.43,7L12,3.43L10.57,2L9.14,3.43L7.71,2L5.57,4.14L4.14,2.71L2.71,4.14L4.14,5.57L2,7.71L3.43,9.14L2,10.57L3.43,12L7,8.43L15.57,17L12,20.57L13.43,22L14.86,20.57L16.29,22L18.43,19.86L19.86,21.29L21.29,19.86L19.86,18.43L22,16.29L20.57,14.86Z";
        // Project & Milestone — briefcase / compass
        public const string icon_project = "M10,2H14A2,2 0 0,1 16,4V6H20A2,2 0 0,1 22,8V19A2,2 0 0,1 20,21H4A2,2 0 0,1 2,19V8A2,2 0 0,1 4,6H8V4A2,2 0 0,1 10,2M14,6V4H10V6H14Z";
        // Rental & Booking — calendar / key
        public const string icon_rental = "M19,19H5V8H19M16,1V3H8V1H6V3H5C3.89,3 3,3.89 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3H18V1M17,12H12V17H17V12Z";
        // Education & Cohort — graduation cap / book
        public const string icon_education = "M12,3L1,9L12,15L21,10.09V17H23V9M5,13.18V17.18L12,21L19,17.18V13.18L12,17L5,13.18Z";

        public static FacilityTypeOption Create(FacilityType type, Guid? id = null, string? customName = null)
        {
            var facilityId = id ?? Guid.Empty;
            return type switch
            {
                FacilityType.PosAndInventory => new FacilityTypeOption
                {
                    Id = facilityId,
                    Type = type,
                    Name = !string.IsNullOrWhiteSpace(customName) ? customName : "POS & Order/Inventory",
                    Description = "Retail, Wholesale, Supermarkets, Food & Beverage",
                    GradientStart = "#6366F1",
                    GradientEnd = "#4338CA",
                    IconKey = icon_pos,
                    IsAvailable = true
                },
                FacilityType.AppointmentAndService => new FacilityTypeOption
                {
                    Id = facilityId,
                    Type = type,
                    Name = !string.IsNullOrWhiteSpace(customName) ? customName : "Appointment & Service",
                    Description = "Salons, Spas, Barbershops, Beauty Clinics",
                    GradientStart = "#EC4899",
                    GradientEnd = "#BE185D",
                    IconKey = icon_appointment,
                    IsAvailable = true
                },
                FacilityType.MembershipAndSession => new FacilityTypeOption
                {
                    Id = facilityId,
                    Type = type,
                    Name = !string.IsNullOrWhiteSpace(customName) ? customName : "Membership & Session",
                    Description = "Gyms, Fitness Studios, Martial Arts, Sports Clubs",
                    GradientStart = "#0EA5E9",
                    GradientEnd = "#0284C7",
                    IconKey = icon_membership,
                    IsAvailable = true
                },
                FacilityType.Gym => new FacilityTypeOption
                {
                    Id = facilityId,
                    Type = type,
                    Name = !string.IsNullOrWhiteSpace(customName) ? customName : "Gym & Fitness",
                    Description = "Fitness & Wellness Analytics",
                    GradientStart = "#0EA5E9",
                    GradientEnd = "#2563EB",
                    IconKey = icon_gym,
                    IsAvailable = true
                },
                FacilityType.Salon => new FacilityTypeOption
                {
                    Id = facilityId,
                    Type = type,
                    Name = !string.IsNullOrWhiteSpace(customName) ? customName : "Salon & Spa",
                    Description = "Beauty & Spa Operations",
                    GradientStart = "#F43F5E",
                    GradientEnd = "#E11D48",
                    IconKey = icon_salon,
                    IsAvailable = true
                },
                FacilityType.Restaurant => new FacilityTypeOption
                {
                    Id = facilityId,
                    Type = type,
                    Name = !string.IsNullOrWhiteSpace(customName) ? customName : "Restaurant",
                    Description = "Fine Dining Control",
                    GradientStart = "#F59E0B",
                    GradientEnd = "#D97706",
                    IconKey = icon_restaurant,
                    IsAvailable = true
                },
                FacilityType.ProjectAndMilestone => new FacilityTypeOption
                {
                    Id = facilityId,
                    Type = type,
                    Name = !string.IsNullOrWhiteSpace(customName) ? customName : "Project & Milestone",
                    Description = "Architecture, Law Firms, Creative Agencies",
                    GradientStart = "#F59E0B",
                    GradientEnd = "#B45309",
                    IconKey = icon_project,
                    IsAvailable = false,
                    BadgeText = "COMING SOON"
                },
                FacilityType.RentalAndBooking => new FacilityTypeOption
                {
                    Id = facilityId,
                    Type = type,
                    Name = !string.IsNullOrWhiteSpace(customName) ? customName : "Rental & Booking",
                    Description = "Coworking Spaces, Event Venues, Equipment Rental",
                    GradientStart = "#10B981",
                    GradientEnd = "#059669",
                    IconKey = icon_rental,
                    IsAvailable = false,
                    BadgeText = "COMING SOON"
                },
                FacilityType.EducationAndCohort => new FacilityTypeOption
                {
                    Id = facilityId,
                    Type = type,
                    Name = !string.IsNullOrWhiteSpace(customName) ? customName : "Education & Cohort",
                    Description = "Training Centers, Academies, Bootcamps, Institutes",
                    GradientStart = "#8B5CF6",
                    GradientEnd = "#6D28D9",
                    IconKey = icon_education,
                    IsAvailable = false,
                    BadgeText = "COMING SOON"
                },
                _ => new FacilityTypeOption
                {
                    Id = facilityId,
                    Type = type,
                    Name = !string.IsNullOrWhiteSpace(customName) ? customName : (type == FacilityType.General ? "General Workspace" : type.ToString()),
                    Description = "Titan Managed Workspace",
                    GradientStart = "#64748B",
                    GradientEnd = "#475569",
                    IconKey = icon_gym,
                    IsAvailable = true
                }
            };
        }
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
