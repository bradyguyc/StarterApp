using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using Microsoft.Identity.Client;
using CommonCode.MSALClient;
using Microsoft.Extensions.Configuration;

namespace MyNextBook.Services
{
    public interface IGetSecrets
    {
        Task<string> GetSecretAsync(string key);
    }

    public class GetSecrets : IGetSecrets
    {
        private string secretsUrl = string.Empty;
        private string secretsScope = string.Empty;
        private readonly Dictionary<string, string> _secrets = new();
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private bool _isInitialized;

        public GetSecrets(IConfiguration configuration)
        {
            _configuration = configuration;


            _httpClient = new HttpClient();

        }

        private async Task EnsureInitialized()
        {
            if (!_isInitialized)
            {
                try
                {
                    var downstreamApiSection = _configuration.GetSection("DownstreamApi");
                    if (!downstreamApiSection.Exists())
                    {
                        throw new InvalidOperationException("DownstreamApi configuration section not found");
                    }

                    secretsUrl = downstreamApiSection.GetValue<string>("secretsUrl") ??
                        throw new ArgumentException("SecretsUrl configuration value cannot be null or empty");

                    secretsScope = downstreamApiSection.GetValue<string>("Scopes") ??
                        throw new ArgumentException("SecretsScope configuration value cannot be null or empty");

                    _isInitialized = await InitGetSecrets();

                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException($"Failed to initialize secrets service: {ex.ToString()}", ex);
                }
            }
        }

        private async Task<bool> InitGetSecrets()
        {
            try
            {
                // Add logging for debugging
                Debug.WriteLine($"Initializing secrets with URL: {secretsUrl}");

                var scopes = new[] { secretsScope };
                string? token;

                try
                {
                    token = await PublicClientSingleton.Instance.AcquireTokenSilentAsync(scopes);
                    Debug.WriteLine("Token acquired successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Token acquisition failed: {ex.ToString()}");
                    throw new InvalidOperationException("Failed to acquire valid token", ex);
                }

                if (string.IsNullOrEmpty(token))
                {
                    throw new InvalidOperationException("Token is null or empty");
                }

                // Set up request
                secretsUrl = $"{secretsUrl}?keyVault=kv-starterapp";
                var request = new HttpRequestMessage(HttpMethod.Get, secretsUrl);
                request.Headers.Accept.Clear();
                request.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

                // Make the request
                try
                {
                    Debug.WriteLine("Sending request to secrets endpoint...");
                    var response = await _httpClient.SendAsync(request);

                    if (!response.IsSuccessStatusCode)
                    {
                        var content = await response.Content.ReadAsStringAsync();
                        Debug.WriteLine($"Request failed: {response.StatusCode}, Content: {response.ToString()}");
                        throw new Exception($"HTTP {response.StatusCode}: {response.ToString()}");
                    }

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine("Secrets retrieved successfully");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"error in InitGetSecrets: {ex.ToString()}\n{ex.StackTrace}");
                    throw new Exception( $"error in InitGetSecrets: {ex.ToString()}\n{ex.StackTrace}",ex);
                    
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in InitGetSecrets: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Debug.WriteLine($"Inner Exception: {ex.InnerException.ToString()}");
                }
                throw new InvalidOperationException($"Error retrieving secrets: {ex.ToString()}", ex);
            }
        }

        public async Task<string> GetSecretAsync(string key)
        {
            try
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
            catch (Exception ex)
            {
                throw new Exception(ex.ToString(), ex);
            }
        }
    }
}
