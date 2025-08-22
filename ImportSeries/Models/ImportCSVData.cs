using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;

using CommonCode.Helpers;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;

using CsvHelper;
using CsvHelper.Configuration;

using ImportSeries.Services;
using ImportSeries.Models;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

using OpenLibraryNET;
using OpenLibraryNET.Data;
using OpenLibraryNET.Loader;
using OpenLibraryNET.OLData;

using Exception = System.Exception;
using MissingFieldException = CsvHelper.MissingFieldException;

namespace ImportSeries.Models;
public partial class ImportCSVData : ObservableObject
{
    public int rowsRead { get; set; } = 0;
    [ObservableProperty] private int seriesFound = 0;
    [ObservableProperty] private int booksFound = 0;
    [ObservableProperty] private int seriesThatAreReadyToImport = 0;
    public string errorMessage { get; set; }

    private readonly IPendingTransactionService _transactionService;
    private readonly IOpenLibraryService _openLibraryService; // injected service
    private int seriesNameColumnIndex = 0; // This class field seems unused by the method being updated.
    [ObservableProperty] public DataTable csvData;
    public Dictionary<string, string> columnHeaderMap = new Dictionary<string, string>();

    public ImportCSVData(IOpenLibraryService transactionAwareOpenLibraryService, IPendingTransactionService transactionService)
    {
        _openLibraryService = transactionAwareOpenLibraryService;
        _transactionService = transactionService;
        csvData = new DataTable();
        errorMessage = "";
    }

    public DataTable GetResultsDataTable()
    {
        if (this.CsvData == null)
        {
            this.CsvData = new DataTable();
        }
        return this.CsvData;
    }

    public async Task<bool> FillInOpenLibraryData(DataTable dataTable)
    {
        try
        {
            // Initialize OpenLibrary client
            var olClient = new OpenLibraryClient();

            // Initialize progress
            int booksProcessed = 0;

            using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<OpenLibraryService>();
            OpenLibraryService ols = new OpenLibraryService(logger, _transactionService);

            // Loop through each row in the DataTable
            Stopwatch stopwatch = new Stopwatch();
            foreach (DataRow row in dataTable.Rows)
            {
                stopwatch.Restart();
                booksProcessed++;
                int progress = (int)((booksProcessed / (double)(SeriesFound + BooksFound)) * 100);
                WeakReferenceMessenger.Default.Send(new ImportProgressMessage($"{booksProcessed} of {BooksFound + SeriesFound}", progress));

                // Extract book information from the row
                string bookTitle = GetColumnValue(row, "Book.Title", "Title");
                string author = GetColumnValue(row, "Book.Author", "Author");
                string publishedDate = GetColumnValue(row, "Book.PublishedDate", "PublishedDate", "Published");
                string isbn10 = GetColumnValue(row, "Book.ISBN_10", "ISBN_10", "ISBN10");
                string isbn13 = GetColumnValue(row, "Book.ISBN_13", "ISBN_13", "ISBN13");
                string olid = GetColumnValue(row, "Book.OLID", "OLID");



                // Ensure ISBN10 is 10 characters long by prepending zeros
                if (!string.IsNullOrWhiteSpace(isbn10) && isbn10.Length < 10)
                {
                    isbn10 = isbn10.PadLeft(10, '0');
                }

                Debug.WriteLine($"[Info] OLSearch Book {bookTitle}");
                // Skip if we don't have enough information to search
                if (string.IsNullOrWhiteSpace(bookTitle) && string.IsNullOrWhiteSpace(isbn10) && string.IsNullOrWhiteSpace(isbn13) && string.IsNullOrWhiteSpace(olid))
                {
                    continue;
                }

                try
                {
                    OLWorkData? workData = null;

                    // 1. Search by OLID
                    if (!string.IsNullOrWhiteSpace(olid))
                    {
                        var results = await OLSearchLoader.GetSearchResultsAsync(
                            olClient.BackingClient,
                            "",
                            new KeyValuePair<string, string>("olid", olid)
                        ).ConfigureAwait(false);
                        if (results != null && results.Length > 0)
                        {
                            workData = results[0];
                            row["OLWork"] = string.IsNullOrEmpty(workData.Key) ? (object)DBNull.Value : workData.Key;
                            row["OLLookupMethod"] = "OLID";
                        }
                    }

                    // 2. Search by ISBN_13
                    if (workData == null && !string.IsNullOrWhiteSpace(isbn13))
                    {
                        // Ensure ISBN13 is exactly 13 digits long
                        if (isbn13.Length == 13 && isbn13.All(char.IsDigit))
                        {
                            OLEditionData? ole = await OLEditionLoader.GetDataByISBNAsync(olClient.BackingClient, isbn13).ConfigureAwait(false);
                            row["OLEdition"] = ole?.Key;

                            if (ole != null && ole.WorkKeys != null && ole.WorkKeys.Count > 0)
                            {
                                row["OLWork"] = ole.WorkKeys[0] ?? (object)DBNull.Value;
                                row["OLLookupMethod"] = "ISBN13";
                            }
                        }
                    }

                    // 3. Search by ISBN_10
                    if (workData == null && !string.IsNullOrWhiteSpace(isbn10))
                    {
                        OLEditionData? ole = await OLEditionLoader.GetDataByISBNAsync(olClient.BackingClient, isbn10).ConfigureAwait(false);
                        row["OLEdition"] = ole?.Key;

                        if (ole != null && ole.WorkKeys != null && ole.WorkKeys.Count > 0)
                        {
                            row["OLWork"] = ole.WorkKeys[0] ?? (object)DBNull.Value;
                            row["OLLookupMethod"] = "ISBN10";
                        }
                    }

                    // 4. Search by title and author
                    if (workData == null && !string.IsNullOrWhiteSpace(bookTitle) && !string.IsNullOrWhiteSpace(author))
                    {
                        var parameters = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("title", bookTitle),
                            new KeyValuePair<string, string>("author", author)
                        };
                        if (!string.IsNullOrWhiteSpace(publishedDate))
                            parameters.Add(new KeyValuePair<string, string>("publish_year", publishedDate));

                        var results = await OLSearchLoader.GetSearchResultsAsync(
                            olClient.BackingClient,
                            "",
                            parameters.ToArray()
                        ).ConfigureAwait(false);
                        if (results != null && results.Length > 0)
                        {
                            workData = results[0];
                            row["OLLookupMethod"] = "Title/Author";
                        }
                    }

                    // 5. Search by title only
                    if (workData == null && !string.IsNullOrWhiteSpace(bookTitle))
                    {
                        var parameters = new List<KeyValuePair<string, string>>
                        {
                            new KeyValuePair<string, string>("title", bookTitle)
                        };
                        if (!string.IsNullOrWhiteSpace(publishedDate))
                            parameters.Add(new KeyValuePair<string, string>("publish_year", publishedDate));

                        var results = await OLSearchLoader.GetSearchResultsAsync(
                            olClient.BackingClient,
                            "",
                            parameters.ToArray()
                        ).ConfigureAwait(false);
                        if (results != null && results.Length > 0)
                        {
                            workData = results[0];
                            row["OLLookupMethod"] = "Title/PublishDate";
                        }
                    }



                }
                catch (Exception ex)
                {
                    // Log error but continue processing other books
                    Debug.WriteLine($"[Info] Error searching for book '{bookTitle}': {ex.Message}");
                    row["OLWork"] = DBNull.Value;
                    row["OLEdition"] = DBNull.Value;
                    row["OLReady"] = false;
                    row["OLLookupMethod"] = "Not Found";
                }

                stopwatch.Stop();
                row["OLLookupTime"] = stopwatch.ElapsedMilliseconds.ToString() + " ms";

                // Add a small delay to avoid overwhelming the API, but use ConfigureAwait(false)
                await Task.Delay(100).ConfigureAwait(false);
            }

            WeakReferenceMessenger.Default.Send(new ImportProgressMessage("OpenLibrary search complete.", 100));
            return true;
        }
        catch (Exception ex)
        {
            errorMessage = $"Error during OpenLibrary search: {ex.Message}";
            Debug.WriteLine($"[Info] Error in FillInOpenLibraryData: {ex.Message}");
            return false;
        }
    }

    private string GetColumnValue(DataRow row, params string[] columnNames)
    {
        foreach (string columnName in columnNames)
        {
            if (row.Table.Columns.Contains(columnName) && row[columnName] != DBNull.Value)
            {
                var value = row[columnName]?.ToString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }
        return string.Empty;
    }

    private string ConvertSeriesGroupToJsonArray(KeyValuePair<string, List<DataRow>> seriesGroup)
    {
        var jsonObjects = new List<Dictionary<string, object>>();

        foreach (var row in seriesGroup.Value)
        {
            var jsonObject = new Dictionary<string, object>();

            foreach (DataColumn column in row.Table.Columns)
            {
                var value = row[column];
                jsonObject[column.ColumnName] = value == DBNull.Value ? null : value;
            }

            jsonObjects.Add(jsonObject);
        }

        return System.Text.Json.JsonSerializer.Serialize(jsonObjects, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public async Task<bool> FillInSeriesDataViaAI(DataTable dataTable)
    {
        // Get AI configuration from AppConfig
        var apiKey = AppConfig.Configuration["AzureOpenAI:ApiKey"];
        var endpoint = AppConfig.Configuration["AzureOpenAI:Endpoint"];
        var deploymentName = AppConfig.Configuration["AzureOpenAI:DeploymentName"];

        var searchAPIKey = AppConfig.Configuration["Google:ApiKey"];
        var googleSearchEngineId = AppConfig.Configuration["Google:SearchEngineId"];

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(deploymentName) || string.IsNullOrEmpty(searchAPIKey) || string.IsNullOrEmpty(googleSearchEngineId))
        {
            errorMessage = "AI configuration is missing from appsettings.json. Please check the keys for AzureOpenAI (ApiKey, Endpoint, DeploymentName) and Google (ApiKey, SearchEngineId).";
            return false;
        }

        var aiChatCompletion = new AIChatCompletion("AzureOpenAI", deploymentName, endpoint, apiKey, "wikipedia", searchAPIKey, googleSearchEngineId);

        int SeriesToProcess = SeriesFound;
        WeakReferenceMessenger.Default.Send(new ImportProgressMessage($"{SeriesToProcess} Series", 0));

        var seriesGroups = GetSeriesGroups(dataTable).ToList();
        int totalSeries = seriesGroups.Count;
        int seriesProcessed = 0;

        // Process each series group one at a time
        Stopwatch st = new Stopwatch();
        foreach (var seriesGroup in seriesGroups)
        {
            var seriesName = seriesGroup.Key;

            seriesProcessed++;
            double progress = (double)seriesProcessed / totalSeries;
            WeakReferenceMessenger.Default.Send(new ImportProgressMessage($"{totalSeries - seriesProcessed} Series", progress));

            // from seriesGroup create a variable called seriesName that is the name of the series in seriesGroup


            // another variable seriesBooks which is a comma seperated list of books in the seriesGroup
            var seriesBooks = string.Join(", ", seriesGroup.Value
                .Where(row => row.Table.Columns.Contains("Book.Title") && row["Book.Title"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["Book.Title"].ToString()))
                .Select(row => row["Book.Title"].ToString()));

            // count of books in the series group
            var bookCount = seriesGroup.Value
                .Count(row => row.Table.Columns.Contains("Book.Title") && row["Book.Title"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["Book.Title"].ToString()));

            // and a variable that is a list of the unique authors in the seriesGroup
            var uniqueAuthors = seriesGroup.Value
                .Where(row => row.Table.Columns.Contains("Book.Author") && row["Book.Author"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["Book.Author"].ToString()))
                .Select(row => row["Book.Author"].ToString()!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            string json = ConvertSeriesGroupToJsonArray(seriesGroup);

            // Capture the row count before AI processing for this series
            int rowsBeforeAI = dataTable.Rows.Count;

            // Call the AI method to enhance the data
            string enhancedData = await aiChatCompletion.FillViaAI(json, seriesName, seriesBooks, uniqueAuthors).ConfigureAwait(false);
            //Debug.WriteLine(enhancedData);
            enhancedData = enhancedData.Replace("```", "");
            enhancedData = enhancedData.Replace("json", "");
            enhancedData = enhancedData.Trim();
            if (enhancedData.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                enhancedData = enhancedData.Substring(4).TrimStart();
            }

            // Process the enhanced data and update the DataTable
            st.Stop();
            UpdateDataTableWithEnhancedData(dataTable, enhancedData, seriesGroup.Key, st);

            // Calculate how many books were added for this series
            int rowsAfterAI = dataTable.Rows.Count;
            int booksAddedForSeries = rowsAfterAI - rowsBeforeAI;

            Debug.WriteLine($"[Info] Processed series: {seriesName} : {st.ElapsedMilliseconds} ms : Books : {bookCount} : Books Added : {booksAddedForSeries}");
        }

        WeakReferenceMessenger.Default.Send(new ImportProgressMessage("Series processing complete.", 1.0));
        return true;
    }

    private void UpdateDataTableWithEnhancedData(DataTable dataTable, string enhancedJson, string seriesName, Stopwatch st)
    {
        try
        {
            // Validate JSON before processing
            List<Dictionary<string, object>>? jsonData;
            try
            {
                //enhancedJson = JsonConvert.DeserializeObject<string>(enhancedJson);
                enhancedJson = enhancedJson.Replace("\\n", "");
                jsonData = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(enhancedJson);
            }
            catch (JsonException ex)
            {
                Debug.WriteLine($"[Info] Invalid JSON received for series '{seriesName}': {ex.Message}");
                Debug.WriteLine($"[Info] Enhanced data: {enhancedJson}");
                return;
            }

            if (jsonData == null) return;

            foreach (var bookData in jsonData)
            {
                try
                {
                    if (bookData == null) continue;

                    var importStatus = bookData.ContainsKey("ImportStatus") ? bookData["ImportStatus"]?.ToString() : null;

                    if (importStatus == "Enhanced")
                    {
                        // Find existing row to update
                        var existingRow = FindMatchingRow(dataTable, bookData, seriesName);
                        if (existingRow != null)
                        {
                            UpdateRowWithJsonData(existingRow, bookData, dataTable, st);
                        }
                    }
                    else if (importStatus == "Add")
                    {
                        // Add new row
                        AddNewRowFromJsonData(dataTable, bookData, st);
                    }
                    else
                    {
                        // Handle cases where ImportStatus might not be present but we still want to update
                        // This ensures ImportNotes and other fields get updated even without explicit ImportStatus
                        var existingRow = FindMatchingRow(dataTable, bookData, seriesName);
                        if (existingRow != null)
                        {
                            UpdateRowWithJsonData(existingRow, bookData, dataTable, st);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Info] Error processing book data: {ex.Message}\n{JsonConvert.SerializeObject(bookData)}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Info] Error processing enhanced data: {ex.Message}\n{enhancedJson}");
        }
    }

    private DataRow? FindMatchingRow(DataTable dataTable, Dictionary<string, object> bookData, string seriesName)
    {
        var seriesRows = dataTable.AsEnumerable()
            .Where(row => row["Series.Name"]?.ToString()?.Equals(seriesName, StringComparison.OrdinalIgnoreCase) == true);

        // First try to match on OLWork if available
        if (bookData.ContainsKey("OLWork") && bookData["OLWork"] != null && !string.IsNullOrWhiteSpace(bookData["OLWork"]?.ToString()))
        {
            var olWorkMatch = seriesRows.FirstOrDefault(row =>
                row["OLWork"] != DBNull.Value &&
                !string.IsNullOrWhiteSpace(row["OLWork"]?.ToString()) &&
                row["OLWork"]?.ToString()?.Equals(bookData["OLWork"]?.ToString(), StringComparison.OrdinalIgnoreCase) == true);

            if (olWorkMatch != null)
                return olWorkMatch;
        }

        // Fall back to title matching if no OLWork match found
        return seriesRows.FirstOrDefault(row =>
            bookData.ContainsKey("Title") ?
            row["Title"]?.ToString()?.Equals(bookData["Title"]?.ToString(), StringComparison.OrdinalIgnoreCase) == true :
            true);
    }

    private void UpdateRowWithJsonData(DataRow row, Dictionary<string, object> bookData, DataTable dataTable, Stopwatch st)
    {
        /*
        foreach (var kvp in bookData)
        {
            if (kvp.Key == "ImportStatus") continue;

            // Add column if it doesn't exist
            if (!dataTable.Columns.Contains(kvp.Key))
            {
                dataTable.Columns.Add(kvp.Key);
            }

            row[kvp.Key] = kvp.Value ?? DBNull.Value;
        }
        */
        row["ImportStatus"] = "Enhanced";
        row["ImportNotes"] = bookData.ContainsKey("ImportNotes") ? bookData["ImportNotes"] ?? DBNull.Value : row["ImportNotes"];
        row["Series Display Order"] = bookData.ContainsKey("Series Display Order") ? bookData["Series Display Order"] ?? DBNull.Value : row["Series Display Order"];

        // Check if ImportNotes contains "ISBNUpdated" and update ISBN fields accordingly
        if (bookData.ContainsKey("ImportNotes") &&
            bookData["ImportNotes"]?.ToString()?.Contains("ISBNUpdated", StringComparison.OrdinalIgnoreCase) == true)
        {
            // Update ISBN_10 field - check multiple possible key names
            var isbn10Keys = new[] { "Book.ISBN_10", "ISBN_10", "ISBN10", "Book.ISBN10" };
            foreach (var key in isbn10Keys)
            {
                if (bookData.ContainsKey(key) && bookData[key] != null)
                {
                    // Ensure the column exists
                    if (!dataTable.Columns.Contains("Book.ISBN_10"))
                    {
                        dataTable.Columns.Add("Book.ISBN_10");
                    }
                    row["Book.ISBN_10"] = bookData[key];
                    break;
                }
            }

            // Update ISBN_13 field - check multiple possible key names
            var isbn13Keys = new[] { "Book.ISBN_13", "ISBN_13", "ISBN13", "Book.ISBN13" };
            foreach (var key in isbn13Keys)
            {
                if (bookData.ContainsKey(key) && bookData[key] != null)
                {
                    // Ensure the column exists
                    if (!dataTable.Columns.Contains("Book.ISBN_13"))
                    {
                        dataTable.Columns.Add("Book.ISBN_13");
                    }
                    row["Book.ISBN_13"] = bookData[key];
                    break;
                }
            }
        }

        row["AILookupTime"] = st.ElapsedMilliseconds.ToString() + " ms";
    }

    private void AddNewRowFromJsonData(DataTable dataTable, Dictionary<string, object> bookData, Stopwatch st)
    {
        var newRow = dataTable.NewRow();

        foreach (var kvp in bookData)
        {
            if (kvp.Key == "ImportStatus") continue;

            // Add column if it doesn't exist
            if (!dataTable.Columns.Contains(kvp.Key))
            {
                dataTable.Columns.Add(kvp.Key);
            }

            newRow[kvp.Key] = kvp.Value ?? DBNull.Value;
        }
        newRow["ImportNotes"] = bookData.ContainsKey("ImportNotes") ? bookData["ImportNotes"] ?? DBNull.Value : newRow["ImportNotes"];
        newRow["Series Display Order"] = bookData.ContainsKey("Series Display Order") ? bookData["Series Display Order"] ?? DBNull.Value : newRow["Series Display Order"];

        newRow["AILookupTime"] = st.ElapsedMilliseconds.ToString() + " ms";
        dataTable.Rows.Add(newRow);
    }



    private IEnumerable<KeyValuePair<string, List<DataRow>>> GetSeriesGroups(DataTable dataTable)
    {
        // Check if Series.Name column exists
        if (!dataTable.Columns.Contains("Series.Name"))
        {
            yield break;
        }

        // Group rows by Series.Name
        var groups = dataTable.AsEnumerable()
            .Where(row => !row.IsNull("Series.Name") && !string.IsNullOrWhiteSpace(row["Series.Name"].ToString()))
            .GroupBy(row => row["Series.Name"].ToString()!.Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key);

        // Return each group one at a time
        foreach (var group in groups)
        {
            yield return new KeyValuePair<string, List<DataRow>>(group.Key, group.ToList());
        }
    }
    public async Task<bool> Import(Stream stream)
    {
        if (stream == null)
        {
            errorMessage = "No stream was provided.";
            return false;
        }
        WeakReferenceMessenger.Default.Send(new ImportProgressMessage($"Loading File", 1));

        bool importedSuccessfully = await PrepFileForCSVImport(stream).ConfigureAwait(false);
        // Debug output: Show current counts
        Debug.WriteLine("[Info] Import Statistics:");
        Debug.WriteLine("[Info]   Total Books: {this.BooksFound}");
        Debug.WriteLine("[Info]   Total Series: {this.SeriesFound}");
        Debug.WriteLine("[Info]   Total Rows in DataTable: {this.CsvData.Rows.Count}");
        WeakReferenceMessenger.Default.Send(new ImportProgressMessage($"Books {BooksFound}", 1));

        if (importedSuccessfully)
        {
            // Search OpenLibrary for book data
            bool olSearchSuccessful = await FillInOpenLibraryData(this.CsvData).ConfigureAwait(false);
            if (!olSearchSuccessful)
            {
                // Log warning but continue with AI processing
                Debug.WriteLine("[Info] OpenLibrary search failed, continuing with AI processing");
            }


            // Count rows with both OLWork and OLEdition populated
            int rowsWithOpenLibraryData = this.CsvData.AsEnumerable()
                .Count(row => row["OLWork"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["OLWork"]?.ToString()) &&
                             row["OLEdition"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["OLEdition"]?.ToString()));
            Debug.WriteLine($"[Info] Rows with OpenLibrary data populated: {rowsWithOpenLibraryData} out of {this.CsvData.Rows.Count}");

            int rowsWithOLWork = this.CsvData.AsEnumerable()
                .Count(row => row["OLWork"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["OLWork"]?.ToString()));

            int rowsWithOLEdition = this.CsvData.AsEnumerable()
                .Count(row => row["OLEdition"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["OLEdition"]?.ToString()));

            Debug.WriteLine($"[Info] Rows with OLWork populated: {rowsWithOLWork} out of {this.CsvData.Rows.Count}");
            Debug.WriteLine($"[Info] Rows with OLEdition populated: {rowsWithOLEdition} out of {this.CsvData.Rows.Count}");

            foreach (var row in this.CsvData.Rows.Cast<DataRow>())
            {
                Debug.WriteLine($" {row["OLReady"]}, OLWork: {row["OLWork"]}, OLEdition: {row["OLEdition"]}, Series.Name: {row["Series.Name"]}, SeriesOrder: {row["Series.BookOrder"]}, Title: {row["Book.Title"]}");
            }


            // Capture original row count before AI processing
            int originalRowCount = this.CsvData.Rows.Count;

            //await FillInViaDBPedia(this.CsvData).ConfigureAwait(false);
            await FillInViaWikidata(this.CsvData).ConfigureAwait(false);

            bool fillSuccessfully = true;
            //  await FillInSeriesDataViaAI(this.CsvData).ConfigureAwait(false);
            // Mark rows with OLWork data as ready
            foreach (DataRow row in this.CsvData.Rows)
            {
                if (row["OLWork"] != DBNull.Value && !string.IsNullOrWhiteSpace(row["OLWork"]?.ToString()))
                {
                    row["OLReady"] = true;
                }
            }

            SeriesThatAreReadyToImport = this.CsvData.AsEnumerable()
                .Where(row => !row.IsNull("Series.Name") && !string.IsNullOrWhiteSpace(row["Series.Name"].ToString()))
                .GroupBy(row => row["Series.Name"].ToString()!.Trim(), StringComparer.OrdinalIgnoreCase)
                .Count(seriesGroup => seriesGroup.All(row =>
                    row["OLReady"] != DBNull.Value &&
                    Convert.ToBoolean(row["OLReady"]) == true));

            // Report new books added by AI
            //ReportNewBooksAdded(originalRowCount); 

            // Debug output: Show current counts
            Debug.WriteLine($"[Info] Import Statistics:");
            Debug.WriteLine($"[Info]   Total Books: {this.BooksFound}");
            Debug.WriteLine($"[Info]   Total Series: {this.SeriesFound}");
            Debug.WriteLine($"[Info]   Total Rows in DataTable: {this.CsvData.Rows.Count}");

#if DEBUG
            // Write DataTable to file on mobile device for debugging
            await WriteDataTableToFileAndCleanup(this.CsvData);
#endif

            return fillSuccessfully;
        }
        return importedSuccessfully;
    }


    async Task<bool> FillInViaWikidata(DataTable dataTable)
    {
        var seriesGroups = GetSeriesGroups(dataTable).ToList();
        if (!seriesGroups.Any()) return true;

        //var isbnLookup = BuildIsbnLookup(dataTable);

        int seriesProcessed = 0;




        Stopwatch stopwatch = new Stopwatch();

        // Process each series group one at a time
        foreach (var seriesGroup in seriesGroups)
        {
            stopwatch.Restart();
            seriesProcessed++;
            int progress = (int)(((seriesProcessed + BooksFound) / (SeriesFound + BooksFound)) * 100);

            WeakReferenceMessenger.Default.Send(new ImportProgressMessage($"Series {seriesProcessed} of  {SeriesFound}", progress));
            var seriesName = seriesGroup.Key;

            // Send progress updates every series or at completion

            try
            {
                List<BookInfo> booksInSeries = WikidataQueryService.GetBooksInSeries(seriesName);

                Debug.WriteLine($"[Info] Wikidata found {booksInSeries.Count} books for series: {seriesName}");

                // Update SeriesOrder based on the order returned from Wikidata
                for (int i = 0; i < booksInSeries.Count; i++)
                {
                    booksInSeries[i].BookOrder = (i + 1).ToString();
                }

                // Process each book from Wikidata
                foreach (var bookInfo in booksInSeries)
                {

                    // Clean up ISBNs - ensure proper format
                    var matchingRows = seriesGroup.Value.Where(row =>
                     (row["Book.Title"]?.ToString())?.Equals(bookInfo.Title, StringComparison.OrdinalIgnoreCase) == true)
                     .ToHashSet();
                    foreach (var row in matchingRows)
                    {
                        // Update row with Wikidata information
                        UpdateRowWithWikidataData(row, bookInfo, dataTable);
                        Debug.WriteLine($"[Info] Updated row with Wikidata data for book: {bookInfo.Title}");
                    }

                    // If no matches found, create a new row with Wikidata data
                    if (!matchingRows.Any())
                    {
                        AddNewRowFromWikidataData(dataTable, bookInfo, seriesName);
                        Debug.WriteLine($"[Info] Added new row from Wikidata for book: {bookInfo.Title}");
                    }
                }

            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Info] Error processing series '{seriesName}' with Wikidata: {ex.Message}");
                throw new Exception($"Error processing series '{seriesName}' with Wikidata: {ex.Message}", ex);
            }

            stopwatch.Stop();

            //Debug.WriteLine($"[Info] Processed series: {seriesName} : {stopwatch.ElapsedMilliseconds} ms : Wikidata books found: {booksInSeries?.Count ?? 0}");
        }

        WeakReferenceMessenger.Default.Send(new ImportProgressMessage("Wikidata search complete.", 1.0));
        return true;
    }

    // --- Modified nullability-safe CleanIsbn signature ---
    private string CleanIsbn(string? isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            return string.Empty;

        // Attempt to parse scientific notation
        if (decimal.TryParse(isbn, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decValue))
        {
            string formattedIsbn = decValue.ToString("F0", CultureInfo.InvariantCulture);
            if (formattedIsbn.Length is 10 or 13)
                return formattedIsbn;
        }

        string digitsOnly = new(isbn.Where(char.IsDigit).ToArray());

        if (digitsOnly.Length == 10)
            return digitsOnly;

        if (digitsOnly.Length == 13)
            return digitsOnly;

        if (digitsOnly == "9780000000000")
            return string.Empty;

        return digitsOnly;
    }





    private void AddNewRowFromWikidataData(DataTable dataTable, BookInfo bookInfo, string seriesName)
    {
        try
        {
            var newRow = dataTable.NewRow();

            // Add Wikidata-specific columns if they don't exist
            if (!dataTable.Columns.Contains("Wikidata.Title"))
                dataTable.Columns.Add("Wikidata.Title", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.Author"))
                dataTable.Columns.Add("Wikidata.Author", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.Publisher"))
                dataTable.Columns.Add("Wikidata.Publisher", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.ReleaseDate"))
                dataTable.Columns.Add("Wikidata.ReleaseDate", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.SeriesOrder"))
                dataTable.Columns.Add("Wikidata.SeriesOrder", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.PreviousWork"))
                dataTable.Columns.Add("Wikidata.PreviousWork", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.SubsequentWork"))
                dataTable.Columns.Add("Wikidata.SubsequentWork", typeof(string));

            // Set basic book information
            newRow["Series.Name"] = seriesName;
            newRow["Wikidata.Title"] = string.IsNullOrWhiteSpace(bookInfo.Title) ? DBNull.Value : bookInfo.Title;
            newRow["Book.Title"] = string.IsNullOrWhiteSpace(bookInfo.Title) ? DBNull.Value : bookInfo.Title;
            newRow["Book.Author"] = string.IsNullOrWhiteSpace(bookInfo.Author) ? DBNull.Value : bookInfo.Author;
            newRow["Series.BookOrder"] = string.IsNullOrWhiteSpace(bookInfo.BookOrder) ? DBNull.Value : bookInfo.BookOrder;
            newRow["Book.ISBN_10"] = string.IsNullOrWhiteSpace(bookInfo.ISBN) ? DBNull.Value : bookInfo.ISBN;
            newRow["Book.ISBN_13"] = string.IsNullOrWhiteSpace(bookInfo.ISBN13) ? DBNull.Value : bookInfo.ISBN13;

            // Set ISBN information
            if (!string.IsNullOrWhiteSpace(bookInfo.ISBN))
            {
                // Try to determine if it's ISBN-10 or ISBN-13 based on length
                string cleanIsbn = CleanIsbn(bookInfo.ISBN);
                if (cleanIsbn.Length == 10)
                {
                    newRow["Book.ISBN_10"] = cleanIsbn;
                }
                else if (cleanIsbn.Length == 13)
                {
                    newRow["Book.ISBN_13"] = cleanIsbn;
                }
            }

            if (!string.IsNullOrWhiteSpace(bookInfo.ISBN13))
            {
                newRow["Book.ISBN_13"] = CleanIsbn(bookInfo.ISBN13);
            }

            // Set DBPedia-specific data
            newRow["Wikidata.Title"] = string.IsNullOrWhiteSpace(bookInfo.Title) ? DBNull.Value : bookInfo.Title;
            newRow["Wikidata.Author"] = string.IsNullOrWhiteSpace(bookInfo.Author) ? DBNull.Value : bookInfo.Author;
            newRow["Wikidata.Publisher"] = string.IsNullOrWhiteSpace(bookInfo.Publisher) ? DBNull.Value : bookInfo.Publisher;
            newRow["Wikidata.ReleaseDate"] = string.IsNullOrWhiteSpace(bookInfo.ReleaseDate) ? DBNull.Value : bookInfo.ReleaseDate;
            newRow["Wikidata.BookOrder"] = string.IsNullOrWhiteSpace(bookInfo.BookOrder) ? DBNull.Value : bookInfo.BookOrder;
            newRow["Wikidata.PreviousWork"] = string.IsNullOrWhiteSpace(bookInfo.PreviousWork) ? DBNull.Value : bookInfo.PreviousWork;
            newRow["Wikidata.SubsequentWork"] = string.IsNullOrWhiteSpace(bookInfo.SubsequentWork) ? DBNull.Value : bookInfo.SubsequentWork;

            // Set import metadata
            newRow["ImportStatus"] = "Added";
            newRow["ImportNotes"] = "WikidataAdded";
            newRow["OLReady"] = false;

            dataTable.Rows.Add(newRow);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Error] Failed to add new row from Wikidata for book '{bookInfo?.Title}' in series '{seriesName}': {ex.Message}");
        }
    }

    private void UpdateRowWithWikidataData(DataRow row, BookInfo bookInfo, DataTable dataTable)
    {
        try
        {
            // Add DBPedia-specific columns if they don't exist
            if (!dataTable.Columns.Contains("Wikidata.Title"))
                dataTable.Columns.Add("Wikidata.Title", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.Author"))
                dataTable.Columns.Add("Wikidata.Author", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.Publisher"))
                dataTable.Columns.Add("Wikidata.Publisher", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.ReleaseDate"))
                dataTable.Columns.Add("Wikidata.ReleaseDate", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.BookOrder"))
                dataTable.Columns.Add("Wikidata.BookOrder", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.PreviousWork"))
                dataTable.Columns.Add("Wikidata.PreviousWork", typeof(string));
            if (!dataTable.Columns.Contains("Wikidata.SubsequentWork"))
                dataTable.Columns.Add("Wikidata.SubsequentWork", typeof(string));

            // Update row with DBPedia data
            row["Wikidata.Title"] = string.IsNullOrWhiteSpace(bookInfo.Title) ? DBNull.Value : bookInfo.Title;
            row["Wikidata.Author"] = string.IsNullOrWhiteSpace(bookInfo.Author) ? DBNull.Value : bookInfo.Author;
            row["Wikidata.Publisher"] = string.IsNullOrWhiteSpace(bookInfo.Publisher) ? DBNull.Value : bookInfo.Publisher;
            row["Wikidata.ReleaseDate"] = string.IsNullOrWhiteSpace(bookInfo.ReleaseDate) ? DBNull.Value : bookInfo.ReleaseDate;
            row["Wikidata.BookOrder"] = string.IsNullOrWhiteSpace(bookInfo.BookOrder) ? DBNull.Value : bookInfo.BookOrder;
            row["Wikidata.PreviousWork"] = string.IsNullOrWhiteSpace(bookInfo.PreviousWork) ? DBNull.Value : bookInfo.PreviousWork;
            row["Wikidata.SubsequentWork"] = string.IsNullOrWhiteSpace(bookInfo.SubsequentWork) ? DBNull.Value : bookInfo.SubsequentWork;

            // Mark that this row was enhanced with DBPedia data
            string currentImportNotes = row["ImportNotes"]?.ToString() ?? "";
            if (!currentImportNotes.Contains("WikidataEnhanced"))
            {
                row["ImportNotes"] = string.IsNullOrWhiteSpace(currentImportNotes)
                    ? "WikidataEnhanced"
                    : currentImportNotes + "; WikidataEnhanced";
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Error] Failed to update row with Wikidata data for book '{bookInfo?.Title}': {ex.Message}");
        }
    }

    public async Task<bool> PrepFileForCSVImport(Stream csvStream)
    {
        try
        {
            this.errorMessage = ""; // Clear previous errors
            this.SeriesFound = 0;
            this.BooksFound = 0;
            this.rowsRead = 0;

            // Clear existing data and columns
            this.CsvData?.Clear();
            this.CsvData?.Columns.Clear();

            if (csvStream == null)
            {
                errorMessage = "No CSV file stream was provided.";
                return false;
            }

            using (var reader = new StreamReader(csvStream))
            {
                var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                {
                    HasHeaderRecord = true,
                    MissingFieldFound = null, // Handle missing fields gracefully if needed
                    HeaderValidated = null, // Optional: Custom header validation
                    TrimOptions = TrimOptions.Trim,
                };

                using (var csvReader = new CsvReader(reader, csvConfig))
                {
                    if (!csvReader.Read() || !csvReader.ReadHeader())
                    {
                        errorMessage = "CSV file is empty or header row is missing.";
                        return false;
                    }

                    var headers = csvReader.HeaderRecord;
                    if (headers == null || !headers.Any())
                    {
                        errorMessage = "CSV header row is missing or empty.";
                        return false;
                    }
                    this.CsvData.Columns.Add("OLReady", typeof(bool));
                    this.CsvData.Columns.Add("OLLookupMethod", typeof(string));
                    this.CsvData.Columns.Add("OLLookupTime", typeof(string));
                    this.CsvData.Columns.Add("AILookupTime", typeof(string));
                    this.CsvData.Columns.Add("ImportNotes", typeof(string));
                    this.CsvData.Columns.Add("ImportStatus", typeof(string));
                    // Add columns to DataTable from CSV headers
                    foreach (var header in headers)
                    {
                        this.CsvData.Columns.Add(header);
                    }

                    int localSeriesNameColumnIndex = -1;
                    for (int i = 0; i < headers.Length; i++)
                    {
                        if (headers[i].Trim().Equals("Series.Name", StringComparison.OrdinalIgnoreCase))
                        {
                            localSeriesNameColumnIndex = i;
                            break;
                        }
                    }

                    var uniqueSeriesNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int rowCount = 0;

                    while (csvReader.Read())
                    {
                        rowCount++;
                        DataRow dataRow = this.CsvData.NewRow();

                        // Set default value for the "Ready" column
                        dataRow["OLReady"] = false;

                        string? currentSeriesName = null; // Renamed to avoid conflict if class field was used

                        for (int i = 0; i < headers.Length; i++)
                        {
                            try
                            {
                                var fieldValue = csvReader.GetField(i);
                                // Use header name to access DataColumn, as "Ready" column shifts indices
                                if (fieldValue != null)
                                {
                                    dataRow[headers[i]] = fieldValue;
                                }
                                else
                                {
                                    dataRow[headers[i]] = DBNull.Value;
                                }
                                if (i == localSeriesNameColumnIndex && !string.IsNullOrWhiteSpace(fieldValue))
                                {
                                    currentSeriesName = fieldValue.Trim();
                                    uniqueSeriesNames.Add(currentSeriesName);
                                }
                            }
                            catch (MissingFieldException)
                            {
                                dataRow[headers[i]] = DBNull.Value;
                            }
                            catch (IndexOutOfRangeException)
                            {
                                dataRow[headers[i]] = DBNull.Value;
                            }
                        }

                        this.CsvData.Rows.Add(dataRow);
                    }

                    CsvData.TableName = "BooksToImport";
                    // Add additional columns at the end

                    this.CsvData.Columns.Add("OLWork", typeof(string));
                    this.CsvData.Columns.Add("OLEdition", typeof(string));
                    //this.CsvData.Columns.Add("OLStatus", typeof(string));

                    this.BooksFound = rowCount;
                    this.SeriesFound = uniqueSeriesNames.Count;
                    this.rowsRead = rowCount;
                }
            }

            if (this.BooksFound == 0 && string.IsNullOrWhiteSpace(this.errorMessage))
            {
                errorMessage = "CSV file contains headers but no data rows.";
            }

            return true;
        }
        catch (HeaderValidationException ex)
        {
            this.errorMessage = $"CSV header validation failed: {ex.Message}";
            this.CsvData?.Clear();
            this.CsvData?.Columns.Clear();
            throw new Exception(this.errorMessage, ex);
        }
        catch (CsvHelperException ex)
        {
            this.errorMessage = $"Error processing CSV file with CsvHelper: {ex.Message}";
            this.CsvData?.Clear();
            this.CsvData?.Columns.Clear();
            throw new Exception(this.errorMessage, ex);
        }
        catch (Exception ex)
        {
            this.errorMessage = $"Unable to read CSV file: {ex.Message}";
            this.CsvData?.Clear();
            this.CsvData?.Columns.Clear();
            throw new Exception(this.errorMessage, ex);
        }
    }



    // --- Modified DEBUG helper to avoid potential UI blocking / runaway perception ---
#if DEBUG
    private async Task WriteDataTableToFileAndCleanup(DataTable dataTable)
    {
        try
        {
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
#if ANDROID || IOS
            documentsPath = FileSystem.AppDataDirectory;
#endif
            string fileName = $"ImportData_Debug_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            string filePath = Path.Combine(documentsPath, fileName);

            Debug.WriteLine($"[Debug] Writing DataTable to file: {filePath}");

            // Offload heavy synchronous IO + cleanup to background thread
            await Task.Run(() =>
            {
                try
                {
                    var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        HasHeaderRecord = true,
                        Encoding = Encoding.UTF8
                    };

                    using (var writer = new StreamWriter(filePath))
                    using (var csvWriter = new CsvWriter(writer, csvConfig))
                    {
                        foreach (DataColumn column in dataTable.Columns)
                            csvWriter.WriteField(column.ColumnName);
                        csvWriter.NextRecord();

                        foreach (DataRow row in dataTable.Rows)
                        {
                            for (int i = 0; i < dataTable.Columns.Count; i++)
                                csvWriter.WriteField(row[i]?.ToString() ?? "");
                            csvWriter.NextRecord();
                        }
                    }

                    Debug.WriteLine($"[Debug] DataTable written. Size: {new FileInfo(filePath).Length} bytes");

                    // Perform in-place cleanup
                    CleanupDataTableColumns(dataTable);
                }
                catch (Exception exInner)
                {
                    Debug.WriteLine($"[Debug] Background write/cleanup error: {exInner.Message}");
                }
            }).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Debug] Error writing DataTable to file: {ex.Message}");
        }
    }

#endif

    // --- Rewritten (now synchronous) CleanupDataTableColumns with nullability fixes & minor efficiency tweaks ---
#if DEBUG
    /// <summary>
    /// Removes all columns except the specified ones and reorders them.
    /// Made synchronous (no real async work) to avoid confusion / potential deadlocks.
    /// </summary>
    private void CleanupDataTableColumns(DataTable dataTable)
    {
        try
        {
            Debug.WriteLine($"[Debug] Starting column cleanup. Original column count: {dataTable.Columns.Count}");

            var columnsToKeep = new[]
            {
                "OLReady",
                "Book.Title",
                "Series.Name",
                "Series.BookOrder",
                "ImportStatus",
                "ImportNotes",
                "OLLookupMethod",
                "Book.ISBN_10",
                "Book.ISBN_13",
                "OLWork",
                "OLEdition",
                "Book.Author",
                "Book.PublishedDate"
            };

            // Fast path: if table empty, just rebuild schema
            bool hasRows = dataTable.Rows.Count > 0;

            var cleanedTable = new DataTable(dataTable.TableName);

            foreach (var columnName in columnsToKeep)
            {
                if (dataTable.Columns.Contains(columnName))
                {
                    var originalColumn = dataTable.Columns[columnName];
#pragma warning disable CS8602
                    cleanedTable.Columns.Add(columnName, originalColumn?.DataType ?? typeof(string)); // Null-safe
#pragma warning restore CS8602
                }
                else
                {
                    cleanedTable.Columns.Add(columnName, typeof(string));
                    Debug.WriteLine($"[Debug] Column '{columnName}' not found; added as string.");
                }
            }

            if (hasRows)
            {
                foreach (DataRow originalRow in dataTable.Rows)
                {
                    var newRow = cleanedTable.NewRow();
                    foreach (var columnName in columnsToKeep)
                    {
                        newRow[columnName] = dataTable.Columns.Contains(columnName)
                            ? originalRow[columnName] ?? DBNull.Value
                            : DBNull.Value;
                    }
                    cleanedTable.Rows.Add(newRow);
                }
            }

            // Normalize ISBN-13 values in the cleaned table before swapping
            if (cleanedTable.Columns.Contains("Book.ISBN_13"))
            {
                foreach (DataRow row in cleanedTable.Rows)
                {
                    if (row["Book.ISBN_13"] != DBNull.Value)
                    {
                        string raw = row["Book.ISBN_13"]?.ToString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(raw))
                        {
                            string isbn13 = CleanIsbn(raw);
                            row["Book.ISBN_13"] = isbn13.Length == 13 ? isbn13 : DBNull.Value;
                        }
                    }
                }
            }

            // Replace original table schema/data atomically
            dataTable.Clear();
            dataTable.Columns.Clear();

            foreach (DataColumn c in cleanedTable.Columns)
                dataTable.Columns.Add(c.ColumnName, c.DataType);

            foreach (DataRow r in cleanedTable.Rows)
            {
                var newRow = dataTable.NewRow();
                foreach (DataColumn c in dataTable.Columns)
                    newRow[c.ColumnName] = r[c.ColumnName];
                dataTable.Rows.Add(newRow);
            }

            Debug.WriteLine($"[Debug] Column cleanup completed. Final column count: {dataTable.Columns.Count}");
            Debug.WriteLine($"[Debug] Final columns: {string.Join(", ", dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName))}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Debug] Error during column cleanup: {ex.Message}");
        }
    }
    // Adds method to create OpenLibrary lists for fully-ready series
    public async Task CreateOpenLibraryListsForReadySeries()
    {
        if (csvData == null) throw new ArgumentNullException(nameof(csvData));

        // Required columns check
        string seriesCol = "Series.Name";
        string readyCol = "OLReady";
        string workCol = "OLWork";
        if (!csvData.Columns.Contains(seriesCol) || !csvData.Columns.Contains(readyCol) || !csvData.Columns.Contains(workCol))
        {
            Debug.WriteLine("[Info] Required columns missing: Series.Name, OLReady, or OLWork.");
            return;
        }

        await _openLibraryService.OLCheckLogedIn();
        OpenLibraryClient olClient = _openLibraryService.GetBackingClient();

        // Group by series and filter to only those where all books are OLReady == true
        var readySeriesGroups = csvData.AsEnumerable()
            .Where(r =>
                r[seriesCol] != DBNull.Value &&
                !string.IsNullOrWhiteSpace(r[seriesCol]?.ToString()))
            .GroupBy(r => r[seriesCol]?.ToString()!.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Any() && g.All(r =>
            {
                try
                {
                    return r[readyCol] != DBNull.Value && Convert.ToBoolean(r[readyCol]) == true;
                }
                catch
                {
                    return false;
                }
            }));

        foreach (var seriesGroup in readySeriesGroups)
        {
            try
            {
                var seriesName = seriesGroup.Key;

                // Collect OLWork ids and display order
                var worksWithOrder = seriesGroup
                    .Where(r => r[workCol] != DBNull.Value && !string.IsNullOrWhiteSpace(r[workCol]?.ToString()))
                    .Select(r => new
                    {
                        id = r[workCol]!.ToString()!,
                        displayOrder = GetDisplayOrderFromRow(r)
                    })
                    .DistinctBy(x => x.id, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x.displayOrder ?? int.MaxValue)
                    .ToList();

                var workIds = worksWithOrder.Select(x => x.id).ToList();

                // Normalize to OpenLibrary seed keys ("/works/OLxxxW")
                static string ToWorkSeed(string id)
                    => id.StartsWith("/works/", StringComparison.OrdinalIgnoreCase) ? id : (id.StartsWith("OL", StringComparison.OrdinalIgnoreCase) ? $"/works/{id}" : id);
                var workSeeds = workIds.Select(ToWorkSeed).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

                // Collect distinct authors
                var authors = new List<string>();
                if (csvData.Columns.Contains("Book.Author"))
                {
                    authors = seriesGroup
                        .Select(r => r["Book.Author"]?.ToString())
                        .Where(s => !string.IsNullOrWhiteSpace(s))
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList()!;
                }

                // Build description JSON
                var descObj = new
                {
                    name = seriesName,
                    description = $"Auto-generated OpenLibrary list for series '{seriesName}' containing {workSeeds.Length} works.",
                    author = authors,
                    olworks = worksWithOrder.Select(x => new { id = ToWorkSeed(x.id), displayorder = x.displayOrder })
                };
                var descriptionJson = System.Text.Json.JsonSerializer.Serialize(descObj);

                string? listId = null;
                try
                {
                    // Check if series exists by name
                    bool exists = await _openLibraryService.OLDoesSeriesExist(seriesName).ConfigureAwait(false);

                    if (exists)
                    {
                        // Find list id
                        var userLists = await OLListLoader.GetUserListsAsync(olClient.BackingClient, olClient.Username).ConfigureAwait(false);
                        var list = userLists?.FirstOrDefault(l => string.Equals(l.Name?.Trim(), seriesName.Trim(), StringComparison.OrdinalIgnoreCase));
                        listId = list?.ID;

                        if (!string.IsNullOrEmpty(listId))
                        {
                            // Get current seeds to compute missing
                            var currentSeeds = await olClient.List.GetListSeedsAsync(olClient.Username, listId).ConfigureAwait(false);
                            var existingSeedKeys = new HashSet<string>((currentSeeds != null ? currentSeeds.Select(s => s.Key) : Enumerable.Empty<string>()), StringComparer.OrdinalIgnoreCase);
                            var missing = workSeeds.Where(seed => !existingSeedKeys.Contains(seed)).ToArray();

                            if (missing.Length > 0)
                            {
                                await olClient.AddSeedsToListAsync(listId, missing).ConfigureAwait(false);
                                Debug.WriteLine($"[Info] Added {missing.Length} missing seeds to existing list '{seriesName}'.");
                            }
                            else
                            {
                                Debug.WriteLine($"[Info] No missing seeds to add for existing list '{seriesName}'.");
                            }
                        }
                    }
                    else
                    {
                        // Create new list including seeds
                        listId = await olClient.CreateListAsync(seriesName, descriptionJson, workSeeds).ConfigureAwait(false);
                        Debug.WriteLine($"[Info] Created new OpenLibrary list '{seriesName}' with {workSeeds.Length} works. ListId: {listId}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[Info] Failed to ensure list for series '{seriesName}': {ex.Message}");
                    continue;
                }



                Debug.WriteLine($"[Info] Ensured OpenLibrary list for series '{seriesName}'. ListId: {listId ?? "N/A"}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Info] Error processing series '{seriesGroup.Key}': {ex.Message}");
            }
        }
    }

    // Attempts to read a reasonable display order from multiple possible columns
    private static int? GetDisplayOrderFromRow(DataRow row)
    {
        // Candidate columns in priority order
        var candidates = new[]  //todo: is this used and why
        {
            "Series.BookOrder",
            "Series Display Order",
            "Wikidata.BookOrder",
            "SeriesOrder",
            "Wikidata.SeriesOrder"
        };

        foreach (var col in candidates)
        {
            if (!row.Table.Columns.Contains(col)) continue;
            var val = row[col];
            if (val == DBNull.Value) continue;

            var s = val?.ToString();
            if (string.IsNullOrWhiteSpace(s)) continue;

            // Extract first integer number present in the value
            var m = Regex.Match(s, @"\d+");
            if (m.Success && int.TryParse(m.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n))
            {
                return n;
            }
            // Try direct int parse
            if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n2))
            {
                return n2;
            }
            // Try to parse decimals but keep integer part
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var dec))
            {
                return (int)Math.Truncate(dec);
            }
        }

        return null;
    }
#endif
}
