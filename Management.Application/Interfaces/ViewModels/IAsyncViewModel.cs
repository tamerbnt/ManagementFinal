using System.Threading.Tasks;

namespace Management.Application.Interfaces.ViewModels
{
    /// <summary>
    /// Represents a ViewModel that requires asynchronous initialization.
    /// This is typically called by the Navigation Service after activation.
    /// </summary>
    public interface IAsyncViewModel
    {
        Task InitializeAsync();
    }
}
