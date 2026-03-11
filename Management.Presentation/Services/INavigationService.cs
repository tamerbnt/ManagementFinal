using System;
using System.Threading.Tasks;
using Management.Presentation.ViewModels;
using Management.Presentation.Extensions;

namespace Management.Presentation.Services
{
    public interface INavigationService
    {
        // Legacy/Sidebar Navigation
        Task NavigateToAsync(int index);

        // Type-Safe Navigation
        Task NavigateToAsync<TViewModel>() where TViewModel : ViewModelBase;
        Task NavigateToAsync<TViewModel>(object parameter) where TViewModel : ViewModelBase;
        
        // Semantic Navigation
        Task NavigateToHomeAsync();

        // Return to Login (special case alias for NavigateToAsync<LoginViewModel>)
        Task NavigateToLoginAsync();
    }
}
