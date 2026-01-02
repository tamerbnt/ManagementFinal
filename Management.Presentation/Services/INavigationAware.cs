using System.Threading.Tasks;

namespace Management.Presentation.Services
{
    public interface INavigationAware
    {
        Task OnNavigatedTo(object parameter);
        Task OnNavigatedFrom();
    }
}
