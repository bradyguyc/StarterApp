using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

using Microsoft.Identity.Client;

using StarterApp.MSALClient;

namespace StarterApp.Services
{
    interface IGetSecrets
    {
        Task InitGetSecrets();
    }
    public class GetSecrets
    {
        public static GetSecrets Instance = new GetSecrets(); // Singleton instance for GetSecrets
        public GetSecrets()
        {
            // InitGetSecrets().GetAwaiter().GetResult(); // Synchronous call to ensure secrets are initialized
            //todo: is this the best way to init
        }
        string secretsUrl = "https://myrecipebookmakerbackend.azurewebsites.net/api/GetSecrets";
        public async Task<bool> InitGetSecrets()
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    secretsUrl = "https://myrecipebookmakerbe.azurewebsites.net/api/GetSecrets";

                    // Define scopes
                    var scopes = new[] { "GetSecrets.Use"
                                                 };
                    string token;

                    try
                    {
                        // Try silent token acquisition first
                        token = await PublicClientSingleton.Instance.AcquireTokenSilentAsync(scopes);
                    }
                    catch (MsalUiRequiredException msalEx)
                    {
                        // If silent fails, try interactive
                        token = await PublicClientSingleton.Instance.MSALClientHelper
                            .SignInUserAndAcquireAccessToken(scopes);
                    }
                    catch (Exception ex)
                    {
                        token = await PublicClientSingleton.Instance.MSALClientHelper
                         .SignInUserAndAcquireAccessToken(scopes);
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
                        Console.WriteLine($"Error retrieving secrets: {response.StatusCode}");
                        throw new Exception($"{response.StatusCode.ToString()} : {await response.Content.ReadAsStringAsync()}");
                    }

                    response.EnsureSuccessStatusCode();

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    // Process the JSON response as needed
                    Console.WriteLine("Secrets retrieved successfully: " + jsonResponse);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error retrieving secrets: " + ex.Message);
                    throw new InvalidOperationException("Error retrieving secrets: " + ex.Message, ex);
                }
            }
        }

        public async Task<bool> InitGetSecrets2()
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    secretsUrl = "https://myrecipebookmakerbe.azurewebsites.net/api/GetSecrets";//?code=T19pzT13bKRum1GPmWCIz-jcjzwxiU0r5DZCQ6ahKx6zAzFujJvXtg==";
                    string token = await PublicClientSingleton.Instance.AcquireTokenSilentAsync();
                    if (token == null)
                    {
                        Console.WriteLine("No access token available. Please sign in first.");
                        throw new InvalidOperationException("No access token available. Please sign in first.");
                    }

                    // Add scopes to the request
                    //var scopes = new[] { "https://myrecipebookmakerbe.azurewebsites.net/.default" };
                    //var authResult = await PublicClientSingleton.Instance.AcquireTokenSilentAsync(scopes);
                    //token = PublicClientSingleton.Instance.MSALClientHelper.AuthResult.AccessToken;
                    var scopes = new[] { "api://7b84f16c-c1b0-4f23-b10c-5fb19dde7c4d/GetSecrets.Read" };
                    var authResult = await PublicClientSingleton.Instance.AcquireTokenSilentAsync(scopes);
                    token = authResult;
                    //var scopes = new[] { "GetSecrets.Read" };
                    //var authResult = await PublicClientSingleton.Instance.AcquireTokenSilentAsync(scopes);

                    httpClient.DefaultRequestHeaders.Accept.Clear();
                    httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    httpClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    var response = await httpClient.GetAsync(secretsUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"Error retrieving secrets: {response.StatusCode}");
                        throw new Exception($"{response.StatusCode.ToString()} : {await response.Content.ReadAsStringAsync()}");
                    }

                    response.EnsureSuccessStatusCode();

                    var jsonResponse = await response.Content.ReadAsStringAsync();
                    // Process the JSON response as needed
                    Console.WriteLine("Secrets retrieved successfully: " + jsonResponse);
                    return true;
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Error retrieving secrets: " + ex.Message);
                    throw new InvalidOperationException("Error retrieving secrets: " + ex.Message, ex);
                }
            }
        }
    }
}
