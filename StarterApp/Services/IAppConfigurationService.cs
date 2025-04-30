using Microsoft.Extensions.Configuration;

namespace StarterApp.Services
{
    public interface IAppConfigurationService
    {
        Task<string> GetConfigurationSettingAsync(string key);
        //Task<bool> GetConfigurationSettingAsync<T>(string key, out T value);
        Task<(bool success, T? value)> GetConfigurationSettingAsync<T>(string key);


        Task<IConfiguration> GetConfigurationAsync();
    }
}
