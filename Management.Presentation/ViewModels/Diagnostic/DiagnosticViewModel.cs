using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Presentation.Extensions;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Domain.Models.Diagnostics;

using Management.Domain.Interfaces;

namespace Management.Presentation.ViewModels.Diagnostic
{
    public partial class DiagnosticViewModel : ObservableObject, IStateResettable
    {
        public void ResetState()
        {
            // Diagnostics are global, no specific reset needed on facility switch
        }
        private readonly IDiagnosticService _diagnosticService;

        [ObservableProperty]
        private Management.Application.Services.DiagnosticEntry? _lastError;

        [ObservableProperty]
        private string _copyButtonText = "Copy Debug Info";

        public ObservableCollection<Management.Application.Services.DiagnosticEntry> Entries { get; } = new();

        public DiagnosticViewModel(IDiagnosticService diagnosticService)
        {
            _diagnosticService = diagnosticService;
            
            // Subscribe to real-time updates
            _diagnosticService.EntryAdded += OnEntryAdded;

            // Initialize with existing entries
            foreach (var entry in _diagnosticService.GetAllEntries())
            {
                Entries.Add(entry);
            }
            
            LastError = Entries.LastOrDefault();
            
            if (LastError == null)
            {
                _ = RefreshAsync();
            }
        }

        private void OnEntryAdded(object? sender, Management.Application.Services.DiagnosticEntry entry)
        {
            // Ensure we update the UI on the correct thread asynchronously to prevent deadlocks
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Entries.Add(entry);
                LastError = entry;
            });
        }

        public async Task RefreshAsync()
        {
            // Check in-memory first (for runtime crashes)
            var last = _diagnosticService.GetAllEntries().LastOrDefault();
            if (last != null)
            {
                LastError = last;
                return;
            }

            LastError = _diagnosticService.GetAllEntries().LastOrDefault();
            
            if (LastError != null)
            {
                // Mark as seen so it doesn't show up again
                // await _diagnosticService.AcknowledgeReportAsync(LastError.Id);
            }
        }

        [RelayCommand]
        private void CopyToClipboard()
        {
            if (LastError == null) return;

            var copyText = $"[{LastError.TimestampUtc}] {LastError.Category} - {LastError.Severity}\n{LastError.Message}\n{LastError.StackTrace}";
            Clipboard.SetText(copyText);

            // Give visual feedback
            CopyButtonText = "Copied!";
            Task.Delay(2000).ContinueWith(_ => CopyButtonText = "Copy Debug Info");
        }

        [RelayCommand]
        private void RestartApplication()
        {
            System.Windows.Application.Current.Shutdown();
            System.Diagnostics.Process.Start(System.Reflection.Assembly.GetEntryAssembly()?.Location.Replace(".dll", ".exe") ?? string.Empty);
        }

        [RelayCommand]
        private void Close()
        {
             System.Windows.Application.Current.Shutdown();
        }

        public void Cleanup()
        {
            _diagnosticService.EntryAdded -= OnEntryAdded;
        }
    }
}
