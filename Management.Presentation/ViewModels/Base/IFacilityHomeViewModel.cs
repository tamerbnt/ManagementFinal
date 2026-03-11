using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using Management.Application.Interfaces.ViewModels;
using Management.Presentation.ViewModels.Shared;

namespace Management.Presentation.ViewModels.Base
{
    /// <summary>
    /// Base interface for all facility-specific Home (Dashboard) ViewModels.
    /// Provides common functionality like Activity Stream, Clock, and Scanning.
    /// </summary>
    public interface IFacilityHomeViewModel : IAsyncViewModel
    {
        ObservableCollection<IActivityItem> ActivityStream { get; }
        
        string ScanInput { get; set; }
        IAsyncRelayCommand ScanCommand { get; }

        string CurrentTime { get; }
        string CurrentDate { get; }
    }
}
