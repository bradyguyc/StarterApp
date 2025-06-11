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
        OLMyBooksData currentlyReading = new();
        OLMyBooksData alreadyRead = new();
        OLMyBooksData wantToRead = new();
        public static List<OLAuthorData> authorsList = new();
        public void SetUsernamePassword(string username, string password)
        {
            OLLoginId = username;
            OLPassword = password;
        }


        public OpenLibraryService(ILogger<OpenLibraryService> logger)
        {
      
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
                        PermitLimit = 15,
                        Window = TimeSpan.FromSeconds(1),
                        QueueLimit = int.MaxValue,
                    }
                );
                if (OLClient == null)
                {
                    OLClient = new OpenLibraryClient(logBuilder: builder => builder.AddDebug());
                    OLClient.BackingClient.Timeout = TimeSpan.FromMinutes(1);
#if ANDROID || IOS
                    OLLoginId = await SecureStorage.Default.GetAsync(Constants.OpenLibraryUsernameKey)
                        .ConfigureAwait(false);
                    OLPassword = await SecureStorage.Default.GetAsync(Constants.OpenLibraryPasswordKey)
                        .ConfigureAwait(false);
                    Debug.WriteLine($"username:{OLLoginId} password: {OLPassword}");
                    EnsureLoggedIn();
                }
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
            if (OLClient == null) InitOpenLibraryService();
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
                await OLGetBookStatus();
                ObservableCollection<Series> bookSeries = new ObservableCollection<Series>();
                OLListData[]? userLists = await OLListLoader
                    .GetUserListsAsync(OLClient.BackingClient, OLClient.Username).ConfigureAwait(false);
                if (userLists != null)
                {
                    foreach (var list in userLists)
                    {
                        Series s = new Series
                        {
                            SeriesData = list,

                            seeds = await OLClient.List.GetListSeedsAsync(OLClient.Username, list.ID),

                        };
                        OLEditionData[] ed = await OLClient.List.GetListEditionsAsync(OLClient.Username, list.ID).ConfigureAwait(false);
                        foreach (var e in ed)
                        {

                            if (e != null)
                            {
                                s.Editions.Add(e);
                            }
                        }

                        foreach (OLSeedData seed in s.seeds)
                        {
                            if (seed.Type == "work")
                            {

                                OLWorkData work = await OLWorkLoader.GetDataAsync(OLClient.BackingClient, seed.ID);
                                OlWorkDataExt w = new OlWorkDataExt();
                                if (work != null)
                                {
                                    CopyProperties(work, w);
                                    s.Works.Add(w); // Add the extended version if you want
                                }
                                foreach (var authorKey in work.AuthorKeys)
                                {
                                    OLAuthorData? author = await OLAuthorLoader.GetDataAsync(OLClient.BackingClient, authorKey);
                                    if (author != null && !authorsList.Contains(author))
                                    {
                                        authorsList.Add(author);
                                    }
                                }

                            }
                            if (seed.Type == "edition")
                            {
                                // Task<(bool, OLEditionData?)> 
                                OLEditionData edition = await OLEditionLoader.GetDataByOLIDAsync(OLClient.BackingClient, seed.ID);

                                if (edition != null)
                                {
                                    s.Editions.Add(edition);
                                }
                            }
                        }
                        //s.StateUpdate();
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

        public async Task OLGetBookStatus()
        {
            try
            {

                currentlyReading = await OLClient.MyBooks.GetCurrentlyReadingAsync(OLClient.Username,
                    new KeyValuePair<string, string>("limit", "500"));
                alreadyRead = await OLClient.MyBooks.GetAlreadyReadAsync(OLClient.Username,
                    new KeyValuePair<string, string>("limit", "500"));
                wantToRead = await OLClient.MyBooks.GetWantToReadAsync(OLClient.Username,
                    new KeyValuePair<string, string>("limit", "500"));

            }


            catch (Exception ex)
            {
                throw new Exception(ex.Message, ex);
            }
        }
        // Utility method to copy all public properties from OLWorkData to OlWorkDataExt using reflection
        public static void CopyProperties<TBase, TDerived>(TBase source, TDerived destination)
            where TBase : class
            where TDerived : class
        {
            var baseType = typeof(TBase);
            var derivedType = typeof(TDerived);

            foreach (var prop in baseType.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance))
            {
                if (!prop.CanRead) continue;
                var derivedProp = derivedType.GetProperty(prop.Name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
                if (derivedProp != null && derivedProp.CanWrite)
                {
                    var value = prop.GetValue(source, null);
                    derivedProp.SetValue(destination, value, null);
                }
            }
        }

    }
}
