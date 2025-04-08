using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;

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
        string secretsUrl = "https://myrecipebookmakerbackend.azurewebsites.net/api/GetSecrets";//?code=tY_0eSojzVVql_c3dooTNHscEgE2gH8qbfitg9nttuwvAzFuZuTE5g==";



        public async Task<bool> InitGetSecrets()
        {
            using (var httpClient = new HttpClient())
            {
                try
                {
                    secretsUrl = "https://testeasyauthbgc.azurewebsites.net/api/GetSecrets?";
                    string token = PublicClientSingleton.Instance.MSALClientHelper.AuthResult.AccessToken;
                    if (token == null)
                    {
                        Console.WriteLine("No access token available. Please sign in first.");
                        throw new InvalidOperationException("No access token available. Please sign in first.");
                    }
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
