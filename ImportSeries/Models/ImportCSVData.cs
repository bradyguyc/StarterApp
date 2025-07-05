using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using CommunityToolkit.Mvvm.ComponentModel;

using CsvHelper;
using CsvHelper.Configuration;

using ImportSeries;


using OpenLibraryNET.Data;

using static System.Net.WebRequestMethods;
using System.Linq;

using Exception = System.Exception;
using MissingFieldException = CsvHelper.MissingFieldException;
using System.Runtime.Serialization;
using CommunityToolkit.Mvvm.Messaging;

namespace ImportSeries.Models;
public partial class ImportCSVData : ObservableObject
{
    public int rowsRead { get; set; } = 0;
    public int SeriesFound { get; set; } = 0;
    public int BooksFound { get; set; } = 0;
    public string errorMessage { get; set; }

    private int seriesNameColumnIndex = 0; // This class field seems unused by the method being updated.
    [ObservableProperty] public DataTable csvData;
    public Dictionary<string, string> columnHeaderMap = new Dictionary<string, string>();
    public ImportCSVData()
    {
        csvData = new DataTable();
        errorMessage = "";
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

        var aiChatCompletion = new AIChatCompletion("AzureOpenAI", deploymentName, endpoint, apiKey, "google,openlibrary", searchAPIKey, googleSearchEngineId);

        int SeriesToProcess = SeriesFound;
        WeakReferenceMessenger.Default.Send(new ImportProgressMessage($"{SeriesToProcess} Series", 0));

        var seriesGroups = GetSeriesGroups(dataTable).ToList();
        int totalSeries = seriesGroups.Count;
        int seriesProcessed = 0;

        // Process each series group one at a time
        foreach (var seriesGroup in seriesGroups)
        {
            seriesProcessed++;
            double progress = (double)seriesProcessed / totalSeries;
            WeakReferenceMessenger.Default.Send(new ImportProgressMessage($"{SeriesToProcess} to Find", progress));

            string json = ConvertSeriesGroupToJsonArray(seriesGroup);

            // Call the AI method to enhance the data
            string enhancedData = await aiChatCompletion.FillViaAI(json);
            //Debug.WriteLine(enhancedData);

            // Process the enhanced data and update the DataTable
            UpdateDataTableWithEnhancedData(dataTable, enhancedData, seriesGroup.Key);
        }

        WeakReferenceMessenger.Default.Send(new ImportProgressMessage("Series processing complete.", 1.0));
        return true;
    }

    private void UpdateDataTableWithEnhancedData(DataTable dataTable, string enhancedJson, string seriesName)
    {
        try
        {
            var jsonData = System.Text.Json.JsonSerializer.Deserialize<List<Dictionary<string, object>>>(enhancedJson);
            
            foreach (var bookData in jsonData)
            {
                if (!bookData.ContainsKey("ImportStatus")) continue;
                
                var importStatus = bookData["ImportStatus"]?.ToString();
                
                if (importStatus == "Enhanced")
                {
                    // Find existing row to update
                    var existingRow = FindMatchingRow(dataTable, bookData, seriesName);
                    if (existingRow != null)
                    {
                        UpdateRowWithJsonData(existingRow, bookData, dataTable);
                    }
                }
                else if (importStatus == "Add")
                {
                    // Add new row
                    AddNewRowFromJsonData(dataTable, bookData);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Error processing enhanced data: {ex.Message}");
        }
    }
    
    private DataRow FindMatchingRow(DataTable dataTable, Dictionary<string, object> bookData, string seriesName)
    {
        return dataTable.AsEnumerable()
            .FirstOrDefault(row => 
                row["Series.Name"]?.ToString()?.Equals(seriesName, StringComparison.OrdinalIgnoreCase) == true &&
                (bookData.ContainsKey("Title") ? row["Title"]?.ToString()?.Equals(bookData["Title"]?.ToString(), StringComparison.OrdinalIgnoreCase) == true : true));
    }
    
    private void UpdateRowWithJsonData(DataRow row, Dictionary<string, object> bookData, DataTable dataTable)
    {
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
    }
    
    private void AddNewRowFromJsonData(DataTable dataTable, Dictionary<string, object> bookData)
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
            .GroupBy(row => row["Series.Name"].ToString().Trim(), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key);

        // Return each group one at a time
        foreach (var group in groups)
        {
            yield return new KeyValuePair<string, List<DataRow>>(group.Key, group.ToList());
        }
    }
    public async Task<bool> Import (Stream stream)
    {
        if (stream == null)
        {
            errorMessage = "No stream was provided.";
            return false;
        }

        bool importedSuccessfully = await PrepFileForCSVImport(stream);
        if (importedSuccessfully)
        {
            bool fillSuccessfully = await FillInSeriesDataViaAI(this.CsvData);
            return fillSuccessfully;
        }
        return importedSuccessfully;
    }

    public async Task<bool> PrepFileForCSVImport(string csvFile)
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

            if (csvFile == null)
            {
                errorMessage = "No CSV file was provided.";
                return false;
            }

            using (var stream = new FileStream(csvFile, FileMode.Open, FileAccess.Read))
            using (var reader = new StreamReader(stream))
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

                        string currentSeriesName = null; // Renamed to avoid conflict if class field was used

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

                        string currentSeriesName = null; // Renamed to avoid conflict if class field was used

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
}
