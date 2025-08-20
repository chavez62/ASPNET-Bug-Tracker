namespace BugTracker.Services
{
    public interface IConfigurationValidator
    {
        bool ValidateAuthenticationConfiguration();
        void LogConfigurationStatus();
    }
}
