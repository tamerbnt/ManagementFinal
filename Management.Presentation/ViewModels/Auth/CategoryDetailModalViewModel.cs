using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Domain.Enums;
using Management.Presentation.Stores;

namespace Management.Presentation.ViewModels.Auth
{
    public class CategoryFeatureItem
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }

    public partial class CategoryDetailModalViewModel : ObservableObject
    {
        private readonly ModalNavigationStore _modalStore;
        private Action<FacilityTypeOption>? _onConfirmed;

        [ObservableProperty]
        private FacilityTypeOption _facility = new();

        [ObservableProperty]
        private string _targetIndustries = string.Empty;

        [ObservableProperty]
        private string _longDescription = string.Empty;

        [ObservableProperty]
        private string _accentColor = "#4F46E5";

        [ObservableProperty]
        private ObservableCollection<CategoryFeatureItem> _features = new();

        [ObservableProperty]
        private ObservableCollection<int> _slides = new();

        [ObservableProperty]
        private int _currentSlideIndex = 0;

        partial void OnCurrentSlideIndexChanged(int value)
        {
            OnPropertyChanged(nameof(CurrentSlideNumber));
        }

        public int CurrentSlideNumber => CurrentSlideIndex + 1;

        public CategoryDetailModalViewModel(ModalNavigationStore modalStore)
        {
            _modalStore = modalStore;
            _slides = new ObservableCollection<int> { 0, 1, 2, 3 };
        }

        public void Configure(FacilityTypeOption facility, Action<FacilityTypeOption> onConfirmed)
        {
            Facility = facility;
            _onConfirmed = onConfirmed;
            CurrentSlideIndex = 0;
            LoadArchetypeDetails(facility.Type);
        }

        private void LoadArchetypeDetails(FacilityType type)
        {
            Features.Clear();

            switch (type)
            {
                case FacilityType.PosAndInventory:
                    AccentColor = "#6366F1";
                    TargetIndustries = "Retail, Wholesale, Supermarkets, Food & Beverage";
                    LongDescription = "Engineered for high-throughput retail workflows, barcode operations, multi-drawer cash tracking, and automated stock replenishment with real-time profit analytics.";
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Point of Sale & Barcode Scanning",
                        Description = "Rapid item scanning, custom SKU modifiers, tender splitting, and dynamic receipt printing."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Live Inventory Matrix & Purchase Orders",
                        Description = "Threshold reorder alerts, supplier catalogs, stock transfers, and automated inventory adjustments."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Shift Tracking & Cash Reconciliations",
                        Description = "Opening float management, end-of-shift cash drops, discrepancy logs, and automated Z-reports."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Offline-First Transaction Vault",
                        Description = "Uninterrupted checkout during internet dropouts with instant background cloud ledger reconciliation."
                    });
                    break;

                case FacilityType.AppointmentAndService:
                    AccentColor = "#EC4899";
                    TargetIndustries = "Salons, Spas, Barbershops, Beauty Clinics";
                    LongDescription = "Tailored for treatment schedules, practitioner rotas, service tier customizations, and deep client consultation records with automated booking reminders.";
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Visual Multi-Specialist Calendar",
                        Description = "Drag-and-drop booking board, room and station scheduling, and multi-staff appointments."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Client Consultation & Intake Histories",
                        Description = "Comprehensive treatment notes, formula records, photo attachments, and visit frequency logs."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Service Menus & Dynamic Add-ons",
                        Description = "Custom duration buffers, pricing levels by practitioner seniority, and package bundling."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Commissions & Gratuity Distribution",
                        Description = "Automated split commissions, individual tip allocation, and staff payroll summaries."
                    });
                    break;

                case FacilityType.MembershipAndSession:
                case FacilityType.Gym:
                    AccentColor = "#0284C7";
                    TargetIndustries = "Gyms, Fitness Studios, Martial Arts, Sports Clubs";
                    LongDescription = "Built for member turnstile validation, recurring membership renewals, trainer-led session management, and club amenity reservations.";
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Automated Turnstile & Gate Access",
                        Description = "Instant RFID, QR, and barcode gate clearance with immediate visual access status feedback."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Recurring Subscriptions & Session Packs",
                        Description = "Tiered auto-renewing plans, prepaid punch-card sessions, family accounts, and guest passes."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Group Class & Coach Scheduling",
                        Description = "Capacity capped classes, dynamic waitlists, coach attendance check-ins, and studio rosters."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Locker & Physical Amenity Allocation",
                        Description = "Physical locker lease tracking, security key audits, and club amenity maintenance logs."
                    });
                    break;

                case FacilityType.ProjectAndMilestone:
                    AccentColor = "#F59E0B";
                    TargetIndustries = "Architecture, Law Firms, Creative Agencies, Consultancies";
                    LongDescription = "Designed for structured client deliverables, milestone progress billing, scope management, and granular billable time tracking.";
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Kanban & Milestone Progress Roadmap",
                        Description = "Deliverable tracking, milestone completion sign-offs, and critical path deadlines."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Billable Hours & Expense Logging",
                        Description = "Precision time entry by project role, billable rate cards, and client reimbursable costs."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Client Review & Sign-Off Portals",
                        Description = "Structured review workflows, revision approvals, and signed scope change amendments."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Milestone-Triggered Invoicing",
                        Description = "Automatic invoice drafting upon milestone completion with multi-stage retainer draws."
                    });
                    break;

                case FacilityType.RentalAndBooking:
                    AccentColor = "#10B981";
                    TargetIndustries = "Coworking Spaces, Event Venues, Equipment Rental, Studios";
                    LongDescription = "Space and resource scheduling engine with real-time double-booking lockout, automated access codes, and turnover maintenance intervals.";
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Space & Resource Allocation Engine",
                        Description = "Unified calendar preventing double bookings across rooms, desks, and specialized gear."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Tiered Rental Pricing & Deposits",
                        Description = "Hourly, daily, and weekly rate tariffs with automated security deposit authorization holds."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Check-in Inspections & Return Logs",
                        Description = "Equipment condition audits, release checklists, and damages liability recording."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Turnover & Cleaning Buffer Windows",
                        Description = "Automated post-booking maintenance blackout buffers before subsequent reservations."
                    });
                    break;

                case FacilityType.EducationAndCohort:
                    AccentColor = "#8B5CF6";
                    TargetIndustries = "Training Centers, Academies, Bootcamps, Institutes";
                    LongDescription = "Complete academic and training management covering student cohort enrollments, attendance ledgers, curriculum tracks, and tuition fee schedules.";
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Cohort Intake & Enrollment Rosters",
                        Description = "Batch student registration, prerequisites verification, and cohort capacity tracking."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Session Attendance & Grading Registers",
                        Description = "Digital attendance roll calls, module rubrics, and continuous student progression tracking."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Classroom & Instructor Allocation",
                        Description = "Faculty schedule management, physical classroom optimization, and lab reservations."
                    });
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Tuition Schedules & Certifications",
                        Description = "Tuition installment tracking, automated payment follow-ups, and completion certificates."
                    });
                    break;

                default:
                    AccentColor = "#0F172A";
                    TargetIndustries = "Titan Managed Workspaces";
                    LongDescription = "Comprehensive enterprise operating workflow tailored to your specific organizational hierarchy.";
                    Features.Add(new CategoryFeatureItem
                    {
                        Title = "Unified Operational Command",
                        Description = "Centralized telemetry, staff permissions, and real-time activity auditing."
                    });
                    break;
            }
        }

        [RelayCommand]
        private void Confirm()
        {
            _onConfirmed?.Invoke(Facility);
            _modalStore.Close();
        }

        [RelayCommand]
        private void Close()
        {
            _modalStore.Close();
        }

        [RelayCommand]
        private void NextSlide()
        {
            if (Slides.Count > 0)
            {
                CurrentSlideIndex = (CurrentSlideIndex + 1) % Slides.Count;
            }
        }

        [RelayCommand]
        private void PrevSlide()
        {
            if (Slides.Count > 0)
            {
                CurrentSlideIndex = (CurrentSlideIndex - 1 + Slides.Count) % Slides.Count;
            }
        }

        [RelayCommand]
        private void SelectSlide(int index)
        {
            if (index >= 0 && index < Slides.Count)
            {
                CurrentSlideIndex = index;
            }
        }
    }
}
