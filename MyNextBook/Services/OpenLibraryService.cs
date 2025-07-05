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
using OpenLibraryNET.Utility;

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
        Task<OLWorkData?> SearchForWorks(
           string booktitle,
           string author,
           string publishedDate,
           string ISBN_10,
           string ISBN_13,
           string OLID);
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

        public async Task InitOpenLibraryService()
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
                }
#endif
            }
            catch (Exception ex)
            {
                ErrorHandler.AddError(ex);
                _logger.LogError(ex, "Failed to initialize OpenLibraryService");
                throw new Exception("Failed to initialize OpenLibraryService", ex);
            }

        }
        /*
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

        */// Initializes the OpenLibraryService with resilience options.


        public async Task<bool> Login()
        {
            try
            {
                if (OLClient.LoggedIn == true)
                {
                    return true; // Already logged in
                }
                if (string.IsNullOrEmpty(OLLoginId) || string.IsNullOrEmpty(OLPassword))
                {
                    throw new Exception("INFO-001 Username or password is not set. Please set them before logging in.");
                }

                await OLClient.LoginAsync(OLLoginId, OLPassword).ConfigureAwait(false);
                if (OLClient.LoggedIn == true)
                {
                    return true;
                }
                else
                {
                    throw new Exception("ERR-003 Login failed. Please check your username and password.");
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.AddError(ex);
                throw new Exception($"ERR-000 Login failed. Please check your username and password. And your network connectivity.\n{ex.Message}", ex);
            }
        }

        public async Task<ObservableCollection<Series>> GetSeries()
        {
            try
            {
                Login();
                await OLGetBookStatus();
                ObservableCollection<Series> bookSeries = new ObservableCollection<Series>();
                OLListData[]? userLists = await OLListLoader
                    .GetUserListsAsync(OLClient.BackingClient, OLClient.Username).ConfigureAwait(false);
                if (userLists != null)
                {
                    foreach (var list in userLists)
                    {
                        if (list.ID != null)
                        {
                            Series s = new Series
                            {
                                SeriesData = list,
                                seeds = await OLClient.List.GetListSeedsAsync(OLClient.Username, list.ID),
                            };
                            OLEditionData[] ed = await OLClient.List.GetListEditionsAsync(OLClient.Username, list.ID).ConfigureAwait(false);
                            if (ed != null)
                            {
                                foreach (var e in ed)
                                {
                                    if (e != null)
                                    {
                                        s.Editions.Add(e);
                                    }
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
                                    OLEditionData edition = await OLEditionLoader.GetDataByOLIDAsync(OLClient.BackingClient, seed.ID);
                                    if (edition != null)
                                    {
                                        s.Editions.Add(edition);
                                    }
                                }
                            }
                            bookSeries.Add(s);
                        }
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

        public async Task<OLWorkData?> SearchForWorks(
            string booktitle,
            string author,
            string publishedDate,
            string ISBN_10,
            string ISBN_13,
            string OLID)
        {
            // 1. Search by OLID
            if (!string.IsNullOrWhiteSpace(OLID))
            {
                var results = await OLSearchLoader.GetSearchResultsAsync(
                    OLClient.BackingClient,
                    "",
                    new KeyValuePair<string, string>("olid", OLID)
                );
                if (results != null && results.Length > 0)
                    return results[0];
            }

            // 2. Search by ISBN_13
            if (!string.IsNullOrWhiteSpace(ISBN_13))
            {
                var results = await OLSearchLoader.GetSearchResultsAsync(
                    OLClient.BackingClient,
                    "",
                    new KeyValuePair<string, string>("isbn", ISBN_13)
                );
                if (results != null && results.Length > 0)
                    return results[0];
            }

            // 3. Search by ISBN_10
            if (!string.IsNullOrWhiteSpace(ISBN_10))
            {
                var results = await OLSearchLoader.GetSearchResultsAsync(
                    OLClient.BackingClient,
                    "",
                    new KeyValuePair<string, string>("isbn", ISBN_10)
                );
                if (results != null && results.Length > 0)
                    return results[0];
            }

            // 4. Search by title and author (and optionally publishedDate)
            if (!string.IsNullOrWhiteSpace(booktitle) && !string.IsNullOrWhiteSpace(author))
            {
                var parameters = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("title", booktitle),
                    new KeyValuePair<string, string>("author", author)
                };
                if (!string.IsNullOrWhiteSpace(publishedDate))
                    parameters.Add(new KeyValuePair<string, string>("publish_year", publishedDate));

                var results = await OLSearchLoader.GetSearchResultsAsync(
                    OLClient.BackingClient,
                    "",
                    parameters.ToArray()
                );
                if (results != null && results.Length > 0)
                    return results[0];
            }

            // 5. Search by title only (and optionally publishedDate)
            if (!string.IsNullOrWhiteSpace(booktitle))
            {
                var parameters = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("title", booktitle)
                };
                if (!string.IsNullOrWhiteSpace(publishedDate))
                    parameters.Add(new KeyValuePair<string, string>("publish_year", publishedDate));

                var results = await OLSearchLoader.GetSearchResultsAsync(
                    OLClient.BackingClient,
                    "",
                    parameters.ToArray()
                );
                if (results != null && results.Length > 0)
                    return results[0];
            }

            // No match found
            return null;
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
