using Azure.Data.AppConfiguration;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace StarterApp.Services
{
    public class AzureAppConfigurationService : IAppConfigurationService
    {
        private readonly Uri _endpoint;
        private readonly ConfigurationClient _client;
        private IConfiguration _configuration;

        public AzureAppConfigurationService(Uri endpoint)
        {
            _endpoint = endpoint;
            _client = new ConfigurationClient(_endpoint, new DefaultAzureCredential());
        }

        public async Task<string> GetConfigurationSettingAsync(string key)
        {
            try
            {
                var setting = await _client.GetConfigurationSettingAsync(key);
                return setting.Value.Value;
            }
            catch
            {
                return null;
            }
        }

        public async Task<(bool success, T? value)> GetConfigurationSettingAsync<T>(string key)
        {
            try
            {
                var setting = await _client.GetConfigurationSettingAsync(key);
                if (setting.Value != null)
                {
                    var convertedValue = (T)Convert.ChangeType(setting.Value.Value, typeof(T));
                    return (true, convertedValue);
                }
            }
            catch
            {
                // Log error if needed
            }
            return (false, default);
        }

        public async Task<IConfiguration> GetConfigurationAsync()
        {
            if (_configuration == null)
            {
                var builder = new ConfigurationBuilder();
                builder.AddAzureAppConfiguration(options =>
                {
                    options.Connect(_endpoint, new DefaultAzureCredential());
                });
                _configuration = builder.Build();
            }
            return _configuration;
        }
    }
}
