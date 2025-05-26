using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.RateLimiting;
using CommonCode.MSALClient;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using MyNextBook.Helpers;
using Microsoft.Maui.Storage;
using OpenLibraryNET;
using OpenLibraryNET.Data;
using OpenLibraryNET.Loader;
using OpenLibraryNET.OLData;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace MyNextBook.Services
{
    public interface IOpenLibraryService
    {
        Task<string> GetOpenLibraryUsernameAsync();
        Task<bool> Login();
        Task<OLListData[]> GetLists();
    }

    public class OpenLibraryService : IOpenLibraryService
    {
        private readonly OpenLibraryClient OLClient;
        private readonly ILogger<OpenLibraryService> _logger;
        private string OLUserName = string.Empty;
       

        public OpenLibraryService(ILogger<OpenLibraryService> logger)
        {
            Debug.WriteLine("in constructor");
            var rateLimiter = new FixedWindowRateLimiter(
                new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 2,
                    Window = TimeSpan.FromSeconds(1),
                    QueueLimit = int.MaxValue,
                }
            );
            OLClient =
                new OpenLibraryClient(
                    configureOptions: options =>
                    
                
                     
                        {
                            options.RateLimiter = new HttpRateLimiterStrategyOptions
                            {
                                Name = $"{nameof(HttpStandardResilienceOptions.RateLimiter)}",
                                RateLimiter = args => rateLimiter.AcquireAsync(cancellationToken: args.Context.CancellationToken)
                            };
                        }
                        // Copy other properties if you add them to BuildResilienceOptions
                    ,
                    logBuilder: builder => builder.AddDebug()// or your preferred logger
                );

            _logger = logger;
        }


        public static class MyResilienceKeys
        {
            public static readonly ResiliencePropertyKey<TimeSpan> SleepDuration = new("SleepDuration");
            public static readonly ResiliencePropertyKey<int> Key2 = new("my-key-2");
        }

        private Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions BuildResilienceOptions()
        {
            var options = new Microsoft.Extensions.Http.Resilience.HttpStandardResilienceOptions
            {
                Retry = new HttpRetryStrategyOptions()
                {

                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                    MaxRetryAttempts = 4,
                    DelayGenerator = async args =>
                    {
                        bool found = args.Context.Properties.TryGetValue(MyResilienceKeys.SleepDuration, out var delay);
                        if (found) ErrorHandler.AddLog($"^^Delay Generator SleepDuration: {delay * 3}");
                        delay = !found ? TimeSpan.FromSeconds(5) : delay * 3;
                        return delay;
                    },
                    OnRetry = async (args) =>
                    {
                        if (args.Outcome.Result is HttpResponseMessage response)
                        {
                            HttpResponseMessage r = (HttpResponseMessage)args.Outcome.Result;
                            ErrorHandler.AddLog($" {OLClient.BackingClient.ToString()} {OLClient.BackingClient.BaseAddress.ToString()}");
                            ErrorHandler.AddLog($"^^Retrying... Attempt: {args.AttemptNumber}, Exception: {args.Outcome.Exception?.Message} \nurl: {r.RequestMessage.RequestUri}");
                        }
                        else
                        {
                            ErrorHandler.AddLog($"^^Retrying... Attempt: {args.AttemptNumber}, Exception: {args.Outcome.Exception?.Message}");
                        }
                        await Task.CompletedTask;
                    }
                }
            };

            // You can add more configuration to options if needed (e.g., Timeout, CircuitBreaker, etc.)

            return options;
        }


        private async Task<bool> EnsureLoggedIn()
        {
            if (OLClient.LoggedIn == false)
            {
                return await Login();
            }

            return true;
        }

        public async Task<bool> Login()
        {
            try
            {
                OLUserName = await SecureStorage.Default.GetAsync(Constants.OpenLibraryUsernameKey);
                string OLPassword = await SecureStorage.Default.GetAsync(Constants.OpenLibraryUsernameKey);

                await OLClient.LoginAsync(OLUserName, OLPassword).ConfigureAwait(false);
                if (OLClient.LoggedIn == true)
                {
                    return true;
                }
                else
                {
                    throw new Exception("Login failed. Please check your username and password.");
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.AddError(ex);
                throw new Exception("Login failed. Please check your username and password.", ex);
            }
        }

        public async Task<string> GetOpenLibraryUsernameAsync()
        {
            try
            {
                string OpenLibraryUsername = SecureStorage.Default.GetAsync(Constants.OpenLibraryUsernameKey).Result ?? string.Empty;
                return OpenLibraryUsername;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving OpenLibrary username from secure storage.");
            }
            return "";
        }

        public async Task<OLListData[]> GetLists()
        {
            if (!await EnsureLoggedIn()) throw new Exception("Openlibrary failed login");

            OLListData[]? userLists = await OLListLoader.GetUserListsAsync(OLClient.BackingClient, OLUserName).ConfigureAwait(false);
            if (userLists != null)
            {
                return userLists; // Return the first list for simplicity
            }

            throw new Exception("No lists found for the user.");
        }
    }
}
