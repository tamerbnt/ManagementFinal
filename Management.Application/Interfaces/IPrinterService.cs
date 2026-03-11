using System.Threading.Tasks;
using Management.Domain.Models;

namespace Management.Application.Interfaces
{
    public interface IPrinterService
    {
        Task PrintTransactionAsync(Transaction transaction);
        Task PrintOrderAsync(Management.Domain.Models.Restaurant.RestaurantOrder order);
        Task OpenCashDrawerAsync();
    }
}
