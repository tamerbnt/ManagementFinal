using System;
using System.ComponentModel;

namespace Management.Application.Interfaces.App
{
    /// <summary>
    /// Tracks the progress of background initialization tasks (DB migrations, hardware sync, license checks)
    /// to signal the onboarding splash screen when it's safe to proceed to login.
    /// </summary>
    public interface IAppInitializationTracker : INotifyPropertyChanged
    {
        bool IsComplete { get; }
        double Progress { get; }
        string StatusMessage { get; }

        void UpdateStatus(string message, double progress);
        void SetComplete();
        void Reset();
    }
}
