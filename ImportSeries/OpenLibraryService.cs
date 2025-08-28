using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.RateLimiting;
using System.Linq;
using OpenLibraryNET.Data; // switch to Data namespace for unified OLWorkData
using OpenLibraryNET.OLData; // ensure OLWorkData from OLData namespace

using ImportSeries.Helpers;
using ImportSeries.Models;
using ImportSeries.Services;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using OpenLibraryNET;
using OpenLibraryNET.Loader;
using OpenLibraryNET.Utility;

namespace ImportSeries
{
    public interface IOpenLibraryService
    {
        Task<bool> Login();
        Task<ObservableCollection<Series>> GetSeries();
        void SetUsernamePassword(string username, string password);
        string OLGetBookStatus(string workKey);
        Task<string?> SearchForEdition(string workOLID, string languageCode = "eng");
        Task OLSetStatus(string ID, string status, DateTimeOffset? readDate = null);
        Task ProcessPendingTransactionsAsync();
        Task OLCheckLogedIn();
        OpenLibraryClient GetBackingClient();
        Task<bool> OLDoesSeriesExist(string seriesName);
        Task<ObservableCollection<OLWorkData>> OLSearchForBook(string title, string author = "", string publisher = "", string publishedYear = "");
    }

    public class OpenLibraryService : IOpenLibraryService
    {
        private OpenLibraryClient OLClient;
        private readonly ILogger<OpenLibraryService> _logger;
        private readonly IPendingTransactionService _transactionService;

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

        public OpenLibraryService(ILogger<OpenLibraryService> logger, IPendingTransactionService transactionService)
        {
            InitOpenLibraryService();
            _logger = logger;
            _transactionService = transactionService;
        }
        public OpenLibraryClient GetBackingClient ()
        {
            return OLClient;
        }
        public async Task InitOpenLibraryService()
        {
            try
            {
                if (OLClient == null)
                {
                    OLClient = new OpenLibraryClient(logBuilder: builder => builder.AddDebug());
                    OLClient.BackingClient.Timeout = TimeSpan.FromMinutes(1);
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.AddError(ex);
                _logger.LogError(ex, "Failed to initialize OpenLibraryService");
                throw new Exception("Failed to initialize OpenLibraryService", ex);
            }
        }
        public  async Task OLCheckLogedIn()
        {
            if ((OLClient != null) && !OLClient.LoggedIn)
            {
                await Login();
            }

            if (!OLClient.LoggedIn)
            {
                throw new Exception("Unable to log in to OpenLibrary");

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

        public string OLGetBookStatus(string workKey)
        {
            if (currentlyReading?.ReadingLogEntries != null &&
                currentlyReading.ReadingLogEntries.Any(entry => entry.Work?.Key == workKey))
            {
                return "Reading";
            }
            if (alreadyRead?.ReadingLogEntries != null &&
                alreadyRead.ReadingLogEntries.Any(entry => entry.Work?.Key == workKey))
            {
                return "Read";
            }
            if (wantToRead?.ReadingLogEntries != null &&
                wantToRead.ReadingLogEntries.Any(entry => entry.Work?.Key == workKey))
            {
                return "To Read";
            }
            return "To Read";
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
                            var s = await GetSeriesForListAsync(OLClient.Username, list.ID).ConfigureAwait(false);
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

        // New helper: get a Series for a specific list by username and listId
        public async Task<Series> GetSeriesForListAsync(string username, string listId)
        {
            Series s = new Series(this);

            // Load basic list data
            var listData = await OLClient.List.GetListAsync(username, listId).ConfigureAwait(false);
            if (listData != null)
            {
                s.SeriesData = listData;
            }

            // Load seeds and editions
            s.seeds = await OLClient.List.GetListSeedsAsync(username, listId).ConfigureAwait(false);
            OLEditionData[] ed = await OLClient.List.GetListEditionsAsync(username, listId).ConfigureAwait(false);
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

            // Populate works and authors from seeds
            foreach (OLSeedData seed in s.seeds)
            {
                if (seed.Type == "work")
                {
                    OLWorkData work = await OLWorkLoader.GetDataAsync(OLClient.BackingClient, seed.ID);
                    if (work != null)
                    {
                        var workExt = new OlWorkDataExt();
                        CopyProperties(work, workExt);
                        workExt.Status = OLGetBookStatus(work.Key);
                        Debug.WriteLine($"Work: {workExt.Title}, Status: {workExt.Status}");
                        s.Works.Add(workExt);
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

            s.UserBooksRead = s.Works.Count(w => OLGetBookStatus(w.Key) == "Read");
            return s;
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

                var earliest = filteredEditions.OrderBy(x => x.publishDate).FirstOrDefault();
                if (earliest.edition.ValueKind != System.Text.Json.JsonValueKind.Undefined &&
                    earliest.edition.TryGetProperty("key", out var keyProperty))
                {
                    var fullKey = keyProperty.GetString();
                    return fullKey?.Split('/').LastOrDefault();
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching for edition with OLID: {workOLID}", workOLID);
                return null;
            }
        }

        public async Task OLSetStatus(string ID, string status, DateTimeOffset? readDate = null)
        {
            try
            {
                string bookshelf_id = status switch
                {
                    "Reading" => "2",
                    "Read" => "3",
                    _ => "1"
                };

                await OLBookUpdateBookshelfData("add", bookshelf_id, ID).ConfigureAwait(false);

                if (bookshelf_id == "3" && readDate != null)
                {
                    await OLUpdateBookFinishedDate(ID, readDate.Value).ConfigureAwait(false);
                }
            }
            catch (Exception ex)
            {
                ErrorHandler.AddError(ex);
                throw new Exception(ex.Message, ex);
            }
        }

        public async Task ProcessPendingTransactionsAsync()
        {
            // Removed MAUI Connectivity dependency. Proceed and rely on HTTP failures to trigger retries.
            try
            {
                if (!OLClient.LoggedIn) await Login();
                if (!OLClient.LoggedIn)
                {
                    _logger.LogWarning("Cannot process pending transactions; OpenLibrary login failed.");
                    return;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during login while processing pending transactions.");
                return;
            }

            var transactions = await _transactionService.GetAllAsync();
            if (transactions.Count == 0) return;

            _logger.LogInformation("Processing {Count} pending transactions.", transactions.Count);

            foreach (var transaction in transactions.ToList())
            {
                bool success = false;
                try
                {
                    switch (transaction.Type)
                    {
                        case TransactionType.UpdateBookshelf:
                            var bookshelfPayload = JsonConvert.DeserializeObject<UpdateBookshelfPayload>(transaction.Payload);
                            await ExecuteUpdateBookshelfAsync(bookshelfPayload.Action, bookshelfPayload.BookshelfId, bookshelfPayload.WorkId);
                            success = true;
                            break;
                        case TransactionType.UpdateFinishedDate:
                            var datePayload = JsonConvert.DeserializeObject<UpdateFinishedDatePayload>(transaction.Payload);
                            await ExecuteUpdateFinishedDateAsync(datePayload.WorkId, datePayload.StatusDate, datePayload.EventId);
                            success = true;
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process transaction {TransactionId}. It will be retried later.", transaction.Id);
                    transaction.RetryCount++;
                    await _transactionService.UpdateAsync(transaction);
                }

                if (success)
                {
                    await _transactionService.RemoveAsync(transaction.Id);
                    _logger.LogInformation("Successfully processed and removed transaction {TransactionId}.", transaction.Id);
                }
            }
        }

        private async Task ExecuteUpdateBookshelfAsync(string action, string bookshelf_id, string workID)
        {
            var c = new MultipartFormDataContent
            {
                { new StringContent(action), "action" },
                { new StringContent(bookshelf_id), "bookshelf_id" },
                { new StringContent(workID), "work_id" },
                { new StringContent("/people/" + OLClient.Username), "user-key" }
            };
            Uri posturi = new Uri($"https://openlibrary.org/works/{workID}/bookshelves.json");
            var response = await OLClient.BackingClient.PostAsync(posturi, c);
            response.EnsureSuccessStatusCode();
        }

        private async Task OLBookUpdateBookshelfData(string action, string bookshelf_id, string workID)
        {
            try
            {
                await ExecuteUpdateBookshelfAsync(action, bookshelf_id, workID);
            }
            catch (Exception ex)
            {
                ErrorHandler.AddError(ex);
                _logger.LogWarning(ex, "Failed to update bookshelf for work {WorkID}. Queuing for later.", workID);

                var payload = new UpdateBookshelfPayload { Action = action, BookshelfId = bookshelf_id, WorkId = workID };
                var transaction = new PendingTransaction
                {
                    Type = TransactionType.UpdateBookshelf,
                    Payload = JsonConvert.SerializeObject(payload)
                };
                await _transactionService.AddAsync(transaction);
            }
        }

        public async Task OLUpdateBookFinishedDate(string workID, DateTimeOffset statusDate, string? eventId = null)
        {
            await ExecuteUpdateFinishedDateAsync(workID, statusDate, eventId);
        }

        private async Task ExecuteUpdateFinishedDateAsync(string workID, DateTimeOffset statusDate, string? eventId = null)
        {
            var url = $"https://openlibrary.org/works/{workID}/check-ins";
            var payload = new
            {
                event_type = 3,
                year = statusDate.Year,
                month = statusDate.Month,
                day = statusDate.Day,
                event_id = eventId
            };

            var jsonPayload = JsonConvert.SerializeObject(payload);
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            content.Headers.ContentType.CharSet = string.Empty;

            var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");

            var response = await OLClient.BackingClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
        }

        private DateTime ParsePublishDate(string publishDate)
        {
            if (DateTime.TryParse(publishDate, out DateTime result))
                return result;

            if (int.TryParse(publishDate, out int year) && year > 1000 && year <= DateTime.Now.Year)
                return new DateTime(year, 1, 1);

            return DateTime.MaxValue;
        }

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

        public async Task<bool> OLDoesSeriesExist(string seriesName)
        {
            if (string.IsNullOrWhiteSpace(seriesName)) return false;
            await OLCheckLogedIn();
            try
            {
                var lists = await OLListLoader.GetUserListsAsync(OLClient.BackingClient, OLClient.Username).ConfigureAwait(false);
                if (lists == null) return false;
                return lists.Any(l => string.Equals(l.Name?.Trim(), seriesName.Trim(), StringComparison.OrdinalIgnoreCase));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to check if series exists: {SeriesName}", seriesName);
                return false;
            }
        }

        public async Task<ObservableCollection<OLWorkData>> OLSearchForBook(string title, string author = "", string publisher = "", string publishedYear = "")
        {
            var results = new ObservableCollection<OLWorkData>();
            try
            {
                await OLCheckLogedIn();
                var paramList = new List<KeyValuePair<string, string>>();
                if (!string.IsNullOrWhiteSpace(title)) paramList.Add(new("title", title));
                if (!string.IsNullOrWhiteSpace(author)) paramList.Add(new("author", author));
                if (!string.IsNullOrWhiteSpace(publisher)) paramList.Add(new("publisher", publisher));
                if (!string.IsNullOrWhiteSpace(publishedYear)) paramList.Add(new("publish_year", publishedYear));
                if (paramList.Count == 0) return results;
                var arr = await OLSearchLoader.GetSearchResultsAsync(OLClient.BackingClient, "", paramList.ToArray()).ConfigureAwait(false);
                if (arr != null)
                    foreach (var w in arr)
                        if (w != null) results.Add(w);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OLSearchForBook failed for Title='{Title}', Author='{Author}', Publisher='{Publisher}', Year='{Year}'", title, author, publisher, publishedYear);
            }
            return results;
        }
    }
}
