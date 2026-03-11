using System.Threading;
using System.Threading.Tasks;

namespace Management.Application.Interfaces.ViewModels
{
    public interface IModalAware
    {
        Task OnModalOpenedAsync(object parameter, CancellationToken cancellationToken = default);
    }

    public interface IModalClosingValidation
    {
        Task<bool> CanCloseAsync(CancellationToken cancellationToken = default);
    }
}
