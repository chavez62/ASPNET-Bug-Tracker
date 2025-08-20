using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BugTracker.Services
{
    public class ConfigurationValidator : IConfigurationValidator
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<ConfigurationValidator> _logger;

        public ConfigurationValidator(IConfiguration configuration, ILogger<ConfigurationValidator> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public bool ValidateAuthenticationConfiguration()
        {
            try
            {
                var authSection = _configuration.GetSection("Authentication");
                
                // Check if authentication section exists
                if (!authSection.Exists())
                {
                    _logger.LogError("Authentication configuration section not found");
                    return false;
                }

                // Validate password requirements
                var requireDigit = authSection.GetValue<bool>("Password:RequireDigit");
                var requireLowercase = authSection.GetValue<bool>("Password:RequireLowercase");
                var requireUppercase = authSection.GetValue<bool>("Password:RequireUppercase");
                var requireNonAlphanumeric = authSection.GetValue<bool>("Password:RequireNonAlphanumeric");
                var requiredLength = authSection.GetValue<int>("Password:RequiredLength");

                // Log configuration values
                _logger.LogInformation("Password Requirements: Digit={Digit}, Lowercase={Lowercase}, Uppercase={Uppercase}, NonAlphanumeric={NonAlphanumeric}, Length={Length}",
                    requireDigit, requireLowercase, requireUppercase, requireNonAlphanumeric, requiredLength);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating authentication configuration");
                return false;
            }
        }

        public void LogConfigurationStatus()
        {
            var isValid = ValidateAuthenticationConfiguration();
            if (isValid)
            {
                _logger.LogInformation("Authentication configuration validation successful");
            }
            else
            {
                _logger.LogWarning("Authentication configuration validation failed");
            }
        }
    }
}
