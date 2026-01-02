using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data; // For CollectionSynchronization
using System.Windows.Input;
using System.Windows.Threading;
// using Management.Presentation.Services; (Already below)
using Management.Application.Stores;
using Management.Presentation.Services;
using Management.Domain.DTOs;
using Management.Domain.Enums; // Assumes TurnstileStatus enum exists here
using Management.Domain.Services;
using Management.Presentation.Extensions;

namespace Management.Presentation.ViewModels
{
    public class AccessControlViewModel : ViewModelBase
    {
        private readonly IAccessEventService _accessEventService;
        private readonly ITurnstileService _turnstileService;
        private readonly Management.Domain.Services.IFacilityContextService _facilityContext;

        // Thread lock for the live feed to safely update from background threads/timers
        private readonly object _feedLock = new object();
        private readonly DispatcherTimer _simulationTimer;
        private readonly Random _random = new Random();

        // --- 1. STATE PROPERTIES ---

        private int _peopleInsideCount;
        public int PeopleInsideCount
        {
            get => _peopleInsideCount;
            set => SetProperty(ref _peopleInsideCount, value);
        }

        // Hardware Collection
        public ObservableCollection<TurnstileItemViewModel> Turnstiles { get; }
            = new ObservableCollection<TurnstileItemViewModel>();

        // Live Log Feed
        public ObservableCollection<AccessEventDto> ActivityFeed { get; }
            = new ObservableCollection<AccessEventDto>();

        // --- 2. COMMANDS ---

        public ICommand SimulateScanCommand { get; }

        // --- 3. CONSTRUCTOR & INITIALIZATION ---

        public AccessControlViewModel(
            IAccessEventService accessEventService,
            ITurnstileService turnstileService,
            Management.Domain.Services.IFacilityContextService facilityContext)
        {
            _accessEventService = accessEventService;
            _turnstileService = turnstileService;
            _facilityContext = facilityContext;

            // Enable cross-thread updates for the Live Feed (Critical for Real-Time apps)
            BindingOperations.EnableCollectionSynchronization(ActivityFeed, _feedLock);

            // Initialize Global Commands
            SimulateScanCommand = new RelayCommand(ExecuteSimulateScan);

            // Initialize Simulation "Heartbeat" (Ticks every 3 seconds)
            _simulationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(3)
            };
            _simulationTimer.Tick += OnSimulationTick;

            // Boot up
            LoadDataAsync();
        }

        private async void LoadDataAsync()
        {
            try
            {
                // 1. Fetch Hardware State
                var hardwareResult = await _turnstileService.GetAllTurnstilesAsync();
                if (hardwareResult.IsSuccess)
                {
                    Turnstiles.Clear();
                    foreach (var t in hardwareResult.Value)
                    {
                        Turnstiles.Add(CreateTurnstileViewModel(t));
                    }
                }

                // 2. Fetch Recent Logs (Last 50)
                var historyResult = await _accessEventService.GetRecentEventsAsync(_facilityContext.CurrentFacilityId, 50);
                if (historyResult.IsSuccess)
                {
                    lock (_feedLock)
                    {
                        ActivityFeed.Clear();
                        foreach (var evt in historyResult.Value) ActivityFeed.Add(evt);
                    }
                }

                // 3. Sync Counters
                var occupancyResult = await _accessEventService.GetCurrentOccupancyAsync(_facilityContext.CurrentFacilityId);
                if (occupancyResult.IsSuccess)
                {
                    PeopleInsideCount = occupancyResult.Value;
                }

                // 4. Start Live Monitoring
                _simulationTimer.Start();
            }
            catch (Exception ex)
            {
                // In production, log this. For now, we assume happy path or simple error handling.
                Console.WriteLine($"Error loading Access Control data: {ex.Message}");
            }
        }

        // --- 4. HARDWARE LOGIC (Turnstiles) ---

        private TurnstileItemViewModel CreateTurnstileViewModel(TurnstileDto dto)
        {
            var vm = new TurnstileItemViewModel
            {
                Id = dto.Id,
                Name = dto.Name,
                Status = dto.Status,
                HardwareId = dto.HardwareId,
                LastHeartbeat = dto.LastHeartbeat,
                ForceOpenCommand = new RelayCommand(async () => await ExecuteTurnstileAction(null!, TurnstileStatus.Operational)), // Replaced with VM closure later if needed, but must set now
                LockDownCommand = new RelayCommand(async () => await ExecuteTurnstileAction(null!, TurnstileStatus.Locked)),
                ResetErrorCommand = new RelayCommand(async () => await ExecuteTurnstileAction(null!, TurnstileStatus.Operational))
            };

            // Re-wire with correct VM reference
            vm.ForceOpenCommand = new RelayCommand(async () => await ExecuteTurnstileAction(vm, TurnstileStatus.Operational));
            vm.LockDownCommand = new RelayCommand(async () => await ExecuteTurnstileAction(vm, TurnstileStatus.Locked));
            vm.ResetErrorCommand = new RelayCommand(async () => await ExecuteTurnstileAction(vm, TurnstileStatus.Operational));

            return vm;
        }

        private async Task ExecuteTurnstileAction(TurnstileItemViewModel turnstile, TurnstileStatus targetStatus)
        {
            // Update UI immediately for responsiveness (Optimistic UI)
            turnstile.Status = targetStatus;

            // Call Hardware Service
            await _turnstileService.UpdateStatusAsync(turnstile.Id, targetStatus);
        }

        // --- 5. LIVE SIMULATION LOGIC (The "Heartbeat") ---

        private void OnSimulationTick(object? sender, EventArgs e)
        {
            ExecuteSimulateScan();
        }

        private void ExecuteSimulateScan()
        {
            if (Turnstiles.Count == 0) return;

            // 1. Pick a random turnstile
            var turnstile = Turnstiles[_random.Next(Turnstiles.Count)];

            // Skip if hardware is offline
            if (turnstile.Status == TurnstileStatus.OutOfOrder) return;

            // 2. Generate Random Event Data
            bool isEntry = _random.NextDouble() > 0.4; // 60% entries, 40% exits
            bool isGranted = _random.NextDouble() > 0.1; // 90% success rate

            var names = new[] { "Tony Stark", "Steve Rogers", "Natasha Romanoff", "Bruce Banner" };
            var evt = new AccessEventDto
            {
                MemberName = names[_random.Next(names.Length)],
                CardId = $"#{_random.Next(10000, 99999)}",
                FacilityName = "Main Entrance",
                Timestamp = DateTime.Now,
                IsAccessGranted = isGranted,
                AccessStatus = isGranted ? "Granted" : (turnstile.Status == TurnstileStatus.Locked ? "Locked" : "Denied"),
                FacilityType = Domain.Enums.FacilityType.Gym
            };

            // 3. Update Feed (Thread-Safe Lock)
            lock (_feedLock)
            {
                ActivityFeed.Insert(0, evt);
                // Maintain buffer size
                if (ActivityFeed.Count > 100) ActivityFeed.RemoveAt(ActivityFeed.Count - 1);
            }

            // 4. Update Counters
            if (isGranted)
            {
                if (isEntry) PeopleInsideCount++;
                else if (PeopleInsideCount > 0) PeopleInsideCount--;
            }
        }
    }

    // --- SUB-VIEWMODEL (Encapsulated Hardware State) ---

    public class TurnstileItemViewModel : ViewModelBase
    {
        public Guid Id { get; set; }
        public required string Name { get; set; }
        public required string HardwareId { get; set; }
        public DateTime LastHeartbeat { get; set; }

        private TurnstileStatus _status;
        public TurnstileStatus Status
        {
            get => _status;
            set => SetProperty(ref _status, value);
        }

        public required ICommand ForceOpenCommand { get; set; }
        public required ICommand LockDownCommand { get; set; }
        public required ICommand ResetErrorCommand { get; set; }
    }
}