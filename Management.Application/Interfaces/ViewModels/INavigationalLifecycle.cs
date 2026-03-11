using System.Threading.Tasks;

namespace Management.Application.Interfaces.ViewModels
{
    /// <summary>
    /// Extends IAsyncViewModel to provide finer control over the navigation sequence.
    /// This allows separating object construction/lighting from heavy data population.
    /// </summary>
    public interface INavigationalLifecycle : IAsyncViewModel
    {
        /// <summary>
        /// Called immediately after construction but BEFORE the View is swapped into the UI.
        /// Use this for lightweight setup (terminology, service resolution).
        /// </summary>
        Task PreInitializeAsync();

        /// <summary>
        /// Called AFTER the View has been swapped into the UI and the transition animation 
        /// is expected to have completed. Use this for heavy collection population / API calls.
        /// </summary>
        Task LoadDeferredAsync();
    }
}
