using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading.RateLimiting;

using CommonCode.MSALClient;

using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Maui.Storage;

using MyNextBook.Helpers;
using MyNextBook.Models;

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
        Task<bool> Login();
        Task<ObservableCollection<Series>> GetSeries();
        void SetUsernamePassword(string username, string password);

    }

    public class OpenLibraryService : IOpenLibraryService
    {
        private OpenLibraryClient OLClient;
        private readonly ILogger<OpenLibraryService> _logger;

        private string OLLoginId = string.Empty;
        private string OLPassword = string.Empty;
        public void SetUsernamePassword(string username, string password)
        {
            OLLoginId = username;
            OLPassword = password;
        }


        public OpenLibraryService(ILogger<OpenLibraryService> logger)
        {
            Debug.WriteLine("in constructor");
            InitOpenLibraryService();

            _logger = logger;
        }
        async Task InitOpenLibraryService()
        {
            try
            {
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
                                RateLimiter = args =>
                                    rateLimiter.AcquireAsync(cancellationToken: args.Context.CancellationToken)
                            };
                        }
                        // Copy other properties if you add them to BuildResilienceOptions
                        ,
                        logBuilder: builder => builder.AddDebug() // or your preferred logger
                    );
#if ANDROID || IOS
                OLLoginId = await SecureStorage.Default.GetAsync(Constants.OpenLibraryUsernameKey).ConfigureAwait(false);
                OLPassword = await SecureStorage.Default.GetAsync(Constants.OpenLibraryPasswordKey).ConfigureAwait(false);
                Debug.WriteLine($"username:{OLLoginId} password: {OLPassword}");
                EnsureLoggedIn();
            }
            catch (Exception ex)
            {
                ErrorHandler.AddError(ex);
                _logger.LogError(ex, "Failed to initialize OpenLibraryService");
                throw new Exception("Failed to initialize OpenLibraryService", ex);
            }
#endif
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
                try
                {
                    if (string.IsNullOrEmpty(OLLoginId) || string.IsNullOrEmpty(OLPassword))
                    {
                        throw new Exception("Username or password is not set. Please set them before logging in.");
                    }
                    return await Login();
                }
                catch (Exception ex)
                {
                    throw new Exception("login failed", ex);
                }
            }

            return true;
        }

        public async Task<bool> Login()
        {
            try
            {

                await OLClient.LoginAsync(OLLoginId, OLPassword).ConfigureAwait(false);
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



        public async Task<ObservableCollection<Series>> GetSeries()
        {
            if (!await EnsureLoggedIn()) throw new Exception("Openlibrary failed login");
            try
            {
                ObservableCollection<Series> bookSeries = new ObservableCollection<Series>();
                OLListData[]? userLists = await OLListLoader
                    .GetUserListsAsync(OLClient.BackingClient, OLClient.Username).ConfigureAwait(false);
                if (userLists != null)
                {
                    foreach (var list in userLists)
                    {
                        Series s = new Series
                        {
                            seriesData = list,

                            seeds = await OLClient.List.GetListSeedsAsync(OLClient.Username, list.ID),

                        };
                        OLEditionData[] ed = await OLClient.List.GetListEditionsAsync(OLClient.Username, list.ID).ConfigureAwait(false);
                        foreach (var e in ed)
                        {

                            if (e != null)
                            {
                                s.editions.Add(e);
                            }
                        }
                      
                        foreach (OLSeedData seed in s.seeds)
                        {
                            if (seed.Type == "work")
                            {

                            }
                            if (seed.Type == "edition")
                            {
                               // Task<(bool, OLEditionData?)> 
                               OLEditionData edition = await OLEditionLoader.GetDataByOLIDAsync(OLClient.BackingClient,seed.ID);
                                 
                                if (edition != null)
                                {
                                    s.editions.Add(edition); 
                                }
                            }
                        }  
                        bookSeries.Add(s);
                    }
                    return bookSeries;
                }
                throw new Exception("No lists found for the user.");
            }
            catch (Exception ex)
            {
                ErrorHandler.AddError(ex);
                _logger.LogError(ex, "Failed to retrieve user lists from OpenLibrary");
                throw new Exception("Failed to retrieve user lists from OpenLibrary", ex);
            }


        }


    }
}
