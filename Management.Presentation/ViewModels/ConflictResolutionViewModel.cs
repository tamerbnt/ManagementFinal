using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Management.Presentation.Extensions;
using Management.Presentation.Services;
using Management.Domain.Primitives;

namespace Management.Presentation.ViewModels
{
    public enum ConflictResolutionResult
    {
        Cancel,
        KeepLocal,
        KeepRemote
    }

    public class ConflictResolutionViewModel : ViewModelBase, IInitializable<object>, IModalResult<ConflictResolutionResult>
    {
        private readonly IModalNavigationService _modalNavigationService;
        
        public string EntityName { get; private set; } = string.Empty;
        public Guid EntityId { get; private set; }
        public string LocalContent { get; private set; } = string.Empty;
        public string RemoteContent { get; private set; } = string.Empty;
        public string? ConflictMessage { get; private set; }

        public ConflictResolutionResult Result { get; private set; } = ConflictResolutionResult.Cancel;
        public bool HasResult => Result != ConflictResolutionResult.Cancel;

        public ICommand UseLocalCommand { get; }
        public ICommand UseRemoteCommand { get; }
        public ICommand CancelCommand { get; }

        public ConflictResolutionViewModel(IModalNavigationService modalNavigationService)
        {
            _modalNavigationService = modalNavigationService;

            UseLocalCommand = new RelayCommand(ExecuteUseLocal);
            UseRemoteCommand = new RelayCommand(ExecuteUseRemote);
            CancelCommand = new RelayCommand(ExecuteCancel);
        }

        public async Task InitializeAsync(object parameter, System.Threading.CancellationToken cancellationToken = default)
        {
            if (parameter is ConflictResolutionParameters p)
            {
                EntityName = p.EntityName;
                EntityId = p.EntityId;
                LocalContent = p.LocalContent;
                RemoteContent = p.RemoteContent;
                ConflictMessage = p.ConflictMessage;
            }
            await Task.CompletedTask;
        }

        private void ExecuteUseLocal()
        {
            Result = ConflictResolutionResult.KeepLocal;
            _modalNavigationService.CloseCurrentModalAsync();
        }

        private void ExecuteUseRemote()
        {
            Result = ConflictResolutionResult.KeepRemote;
            _modalNavigationService.CloseCurrentModalAsync();
        }

        private void ExecuteCancel()
        {
            Result = ConflictResolutionResult.Cancel;
            _modalNavigationService.CloseCurrentModalAsync();
        }
    }

    public class ConflictResolutionParameters
    {
        public string EntityName { get; set; } = string.Empty;
        public Guid EntityId { get; set; }
        public string LocalContent { get; set; } = string.Empty;
        public string RemoteContent { get; set; } = string.Empty;
        public string? ConflictMessage { get; set; }
    }
}
