namespace Management.Application.Services
{
    public interface ISessionTracker
    {
        void StartSession();
        void EndSession();
        bool WasLastSessionClean();
    }
}
