using Microsoft.Extensions.Logging;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Management.Presentation.Extensions;
using Management.Application.Services;
using Management.Application.Interfaces.App;
using Management.Presentation.Services;

namespace Management.Presentation.ViewModels.AccessControl
{
    public partial class AccessControlViewModel : ViewModelBase
    {
        [ObservableProperty]
        private int _peopleInsideCount;

        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand GrantAccessCommand { get; }
        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand DenyAccessCommand { get; }
        public CommunityToolkit.Mvvm.Input.IRelayCommand ScanCardCommand { get; }

        public CommunityToolkit.Mvvm.Input.IAsyncRelayCommand SimulateScanCommand { get; }

        public AccessControlViewModel(
            ILogger<AccessControlViewModel> logger,
            IDiagnosticService diagnosticService,
            IToastService toastService)
            : base(logger, diagnosticService, toastService)
        {
            Title = "Access Control & Occupancy";
            var asyncCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => Task.CompletedTask);
            SimulateScanCommand = asyncCommand;
            SimulateScanCommand = new CommunityToolkit.Mvvm.Input.AsyncRelayCommand(() => Task.CompletedTask);
        }
    }
}
