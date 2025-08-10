using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using System.Threading.RateLimiting;

using CommonCode.Helpers;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

using ImportSeries.Models;

using OpenLibraryNET;
using OpenLibraryNET.Data;
using OpenLibraryNET.Loader;
using OpenLibraryNET.OLData;
using OpenLibraryNET.Utility;

using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;

namespace ImportSeries.Services
{
    public interface IOpenLibraryService
    {
        Task<bool> Login();
        Task<ObservableCollection<Series>> GetSeries();
        void SetUsernamePassword(string username, string password);
        Task<(OLWorkData?,string?)> SearchForWorks(
           string booktitle,
           string author,
           string publishedDate,
           string ISBN_10,
           string ISBN_13,
           string OLID);
        Task<string?> SearchForEdition(string workOLID, string languageCode = "eng");
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
            _logger = logger;
            InitOpenLibraryService();
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
                    
                    // Note: SecureStorage is not available in test projects, credentials need to be set manually
                    // For testing, use SetUsernamePassword method
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.AddError(ex);
                _logger.LogError(ex, "Failed to initialize OpenLibraryService");
                throw new Exception("Failed to initialize OpenLibraryService", ex);
            }
        }

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
                await Login();
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
                            Series s = new Series()
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
        public async Task<string?> SearchForEdition(string workOLID, string languageCode = "eng")
        {
            try
            {
                using var httpClient = new HttpClient();
                var url = $"https://openlibrary.org/works/{workOLID}/editions.json";
                var response = await httpClient.GetStringAsync(url);
                
                var editionsData = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(response);
                var entries = editionsData.GetProperty("entries");
                
                var filteredEditions = new List<(System.Text.Json.JsonElement edition, DateTime publishDate)>();
                
                foreach (var entry in entries.EnumerateArray())
                {
                    // Check for language
                    bool hasTargetLanguage = false;
                    if (entry.TryGetProperty("languages", out var languages))
                    {
                        foreach (var lang in languages.EnumerateArray())
                        {
                            if (lang.TryGetProperty("key", out var key) && 
                                key.GetString()?.Contains($"/languages/{languageCode}") == true)
                            {
                                hasTargetLanguage = true;
                                break;
                            }
                        }
                    }
                    
                    if (!hasTargetLanguage) continue;
                    
                    // Get publish date
                    if (entry.TryGetProperty("publish_date", out var publishDateProp))
                    {
                        var publishDateStr = publishDateProp.GetString();
                        if (!string.IsNullOrWhiteSpace(publishDateStr))
                        {
                            var parsedDate = ParsePublishDate(publishDateStr);
                            filteredEditions.Add((entry, parsedDate));
                        }
                    }
                }
                
                // Return the OLID of the earliest edition
                var earliest = filteredEditions.OrderBy(x => x.publishDate).FirstOrDefault();
                if (earliest.edition.ValueKind != System.Text.Json.JsonValueKind.Undefined &&
                    earliest.edition.TryGetProperty("key", out var keyProperty))
                {
                    var fullKey = keyProperty.GetString();
                    // Extract OLID from the key (format: "/books/OL123456M")
                    return fullKey?.Split('/').LastOrDefault();
                }
                
                throw new Exception("No editions found for the specified work with the target language.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for edition with OLID: {workOLID}", workOLID);
                return null;
            }
        }

        private DateTime ParsePublishDate(string publishDate)
        {
            // Try to parse various date formats
            if (DateTime.TryParse(publishDate, out DateTime result))
                return result;

            // Try to extract year if it's just a year
            if (int.TryParse(publishDate, out int year) && year > 1000 && year <= DateTime.Now.Year)
                return new DateTime(year, 1, 1);

            // Return max value if unparseable (will be sorted last)
            return DateTime.MaxValue;
        }
        public async Task<(OLWorkData?,string?)> SearchForWorks(
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
                    return ( results[0],"OLID");
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
                    return (results[0],"ISBN13");
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
                    return (results[0],"ISBN10");
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
                    return (results[0],"Title/Author");
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
                    return (results[0],"Title/PublishYear");
            }

            // No match found
            throw new Exception("No matching work found for the provided criteria.");
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
