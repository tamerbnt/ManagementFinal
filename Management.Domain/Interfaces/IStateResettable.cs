namespace Management.Domain.Interfaces
{
    /// <summary>
    /// Interface for services and stores that contain facility-specific state
    /// and must be reset when switching facilities.
    /// </summary>
    public interface IStateResettable
    {
        /// <summary>
        /// Resets the internal state of the component to its default/empty state.
        /// This is called during a facility switch to prevent data leaking.
        /// </summary>
        void ResetState();
    }
}
