using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Domain.Services;
using Management.Presentation.Extensions;
using Management.Presentation.Models;
using Management.Presentation.Services;

namespace Management.Presentation.ViewModels
{
    public class DiagnosticViewModel : ViewModelBase
    {
        private readonly IDiagnosticService _diagnosticService;
        private CancellationTokenSource? _streamCts;

        public ObservableCollection<DiagnosticEntry> Entries { get; } = new();

        private DiagnosticCategory? _selectedCategory;
        public DiagnosticCategory? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (SetProperty(ref _selectedCategory, value))
                {
                    ApplyFilter();
                }
            }
        }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (SetProperty(ref _searchText, value))
                {
                    ApplyFilter();
                }
            }
        }

        private int _totalCount;
        public int TotalCount
        {
            get => _totalCount;
            set => SetProperty(ref _totalCount, value);
        }

        private int _errorCount;
        public int ErrorCount
        {
            get => _errorCount;
            set => SetProperty(ref _errorCount, value);
        }

        private int _warningCount;
        public int WarningCount
        {
            get => _warningCount;
            set => SetProperty(ref _warningCount, value);
        }

        public ICommand ClearAllCommand { get; }
        public ICommand ShowAllCommand { get; }
        public ICommand ShowBindingCommand { get; }
        public ICommand ShowDICommand { get; }
        public ICommand ShowNetworkCommand { get; }
        public ICommand ShowThemeCommand { get; }
        public ICommand ShowRuntimeCommand { get; }
        public ICommand ExportCommand { get; }

        private readonly IDialogService _dialogService;
        private readonly ITerminologyService _terminologyService;

        public DiagnosticViewModel(IDiagnosticService diagnosticService, IDialogService dialogService, ITerminologyService terminologyService)
        {
            _diagnosticService = diagnosticService;
            _dialogService = dialogService;
            _terminologyService = terminologyService;

            ClearAllCommand = new RelayCommand(ExecuteClearAll);
            ShowAllCommand = new RelayCommand(() => SelectedCategory = null);
            ShowBindingCommand = new RelayCommand(() => SelectedCategory = DiagnosticCategory.Binding);
            ShowDICommand = new RelayCommand(() => SelectedCategory = DiagnosticCategory.DependencyInjection);
            ShowNetworkCommand = new RelayCommand(() => SelectedCategory = DiagnosticCategory.Network);
            ShowThemeCommand = new RelayCommand(() => SelectedCategory = DiagnosticCategory.Theme);
            ShowRuntimeCommand = new RelayCommand(() => SelectedCategory = DiagnosticCategory.Runtime);
            ExportCommand = new AsyncRelayCommand(ExecuteExportAsync);

            // Subscribe to new entries
            _diagnosticService.EntryAdded += OnEntryAdded;

            // Load existing entries
            LoadExistingEntries();

            // Start streaming new entries
            _ = StartErrorStreamAsync();
        }

        private void LoadExistingEntries()
        {
            var existing = _diagnosticService.GetAllEntries();
            foreach (var entry in existing)
            {
                Entries.Add(entry);
            }
            UpdateCounts();
        }

        private async Task StartErrorStreamAsync()
        {
            _streamCts = new CancellationTokenSource();
            
            try
            {
                await foreach (var entry in _diagnosticService.GetErrorStreamAsync(_streamCts.Token))
                {
                    // Entry already added via event, just update counts
                    UpdateCounts();
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when window closes
            }
        }

        private void OnEntryAdded(object? sender, DiagnosticEntry entry)
        {
            // Add to UI thread
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                Entries.Insert(0, entry); // Add to top for latest-first display
                UpdateCounts();
                ApplyFilter();
            });
        }

        private void ApplyFilter()
        {
            // Note: For better performance with large datasets, consider using CollectionViewSource
            // For now, we'll keep all items in the collection and let the view handle filtering
            UpdateCounts();
        }

        private void UpdateCounts()
        {
            TotalCount = Entries.Count;
            ErrorCount = Entries.Count(e => e.Severity == DiagnosticSeverity.Error || e.Severity == DiagnosticSeverity.Critical);
            WarningCount = Entries.Count(e => e.Severity == DiagnosticSeverity.Warning);
        }

        private void ExecuteClearAll()
        {
            Entries.Clear();
            _diagnosticService.ClearAll();
            UpdateCounts();
        }

        private async Task ExecuteExportAsync()
        {
            await Task.Run(() =>
            {
                var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var filename = $"DiagnosticLog_{timestamp}.txt";
                var path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), filename);

                var lines = Entries.Select(e => 
                    $"[{e.TimeDisplay}] [{e.CategoryDisplay}] [{e.SeverityDisplay}] {e.MethodName}: {e.Message}" +
                    (e.HasStackTrace ? $"\n{e.StackTrace}\n" : "")
                );

                System.IO.File.WriteAllLines(path, lines);

                System.Windows.Application.Current?.Dispatcher.Invoke(async () =>
                {
                    await _dialogService.ShowAlertAsync(
                        $"{_terminologyService.GetTerm("TerminologyDiagnosticExportedLabel")}:\n{path}",
                        _terminologyService.GetTerm("TerminologyExportCompleteLabel")
                    );
                });
            });
        }

        public void Cleanup()
        {
            _streamCts?.Cancel();
            _diagnosticService.EntryAdded -= OnEntryAdded;
        }
    }
}
