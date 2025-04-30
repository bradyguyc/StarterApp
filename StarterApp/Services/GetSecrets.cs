using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using Microsoft.Identity.Client;
using StarterApp.MSALClient;
using Microsoft.Extensions.Configuration;

namespace StarterApp.Services
{
    public interface IGetSecrets
    {
        Task<string> GetSecretAsync(string key);
    }
  
    public class GetSecrets : IGetSecrets
    {
        private string secretsUrl = string.Empty;
        private string secretsScope = string.Empty;
        private readonly IAppConfigurationService _configuration;
        private readonly Dictionary<string, string> _secrets = new();
        private bool _isInitialized;

        public GetSecrets(IAppConfigurationService configuration)
        {
            _configuration = configuration;
            // Don't call InitGetSecrets directly in constructor
            // as it's async and could fail
        }

        private async Task EnsureInitialized()
        {
            if (!_isInitialized)
            {
                try
                {
                    secretsUrl = await _configuration.GetConfigurationSettingAsync("SecretsUrl");
                    secretsScope = await _configuration.GetConfigurationSettingAsync("SecretsScope");
                    await InitGetSecrets();
                    _isInitialized = true;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Failed to initialize secrets service", ex);
                }
            }
        }

        private async Task<bool> InitGetSecrets()
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    secretsUrl = "https://myrecipebookmakerbe.azurewebsites.net/api/GetSecrets";

                    // Define scopes
                    var scopes = new[] { "openid offline_access api://7b84f16c-c1b0-4f23-b10c-5fb19dde7c4d/GetSecrets.Read" };
                    string token;

                    try
                    {
                        // Try silent token acquisition first
                        token = await PublicClientSingleton.Instance.AcquireTokenSilentAsync(scopes);
                    }
                    catch
                    {
                        throw new InvalidOperationException("Failed to acquire valid token");
                    }

                    if (string.IsNullOrEmpty(token))
                    {
                        throw new InvalidOperationException("Failed to acquire valid token");
                    }

                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    var response = await httpClient.GetAsync(secretsUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new Exception($"{response.StatusCode} : {await response.Content.ReadAsStringAsync()}");
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    // TODO: Parse jsonResponse into _secrets dictionary
                    // _secrets = JsonSerializer.Deserialize<Dictionary<string, string>>(jsonResponse);
                    
                    return true;
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("Error retrieving secrets", ex);
                }
            }
        }

        public async Task<string> GetSecretAsync(string key)
        {
            await EnsureInitialized();

            // Once initialized, check if we have the secret in our dictionary
            if (_secrets.TryGetValue(key, out var secret))
            {
                return secret;
            }

            // For now, keeping the dummy implementation
            await Task.Delay(100);
            return $"Secret value for key: {key} - {Guid.NewGuid()}";
        }
    }
}
