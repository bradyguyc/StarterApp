using System.Collections.ObjectModel;
using System.Data;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

using MyNextBook.Helpers;
using MyNextBook.Models;
using MyNextBook.ViewModels;

using OpenLibraryNET.Data;
using Exception = System.Exception;
using CsvHelper;
using System.Globalization;
using CsvHelper.Configuration;
using MissingFieldException = CsvHelper.MissingFieldException;

public partial class ImportCSVData : ObservableObject
{
    //public ObservableCollection<Series> csvData { get; set; }
    public ObservableCollection<CsvHeaders> headerProperties { get; set; }
    public ObservableCollection<CsvHeaders> unknownProperties { get; set; }
    public int rowsRead { get; set; } = 0;
    public int SeriesFound { get; set; } = 0;
    public int BooksFound { get; set; } = 0;
    public string errorMessage { get; set; }

    private int seriesNameColumnIndex = 0; // This class field seems unused by the method being updated.
    [ObservableProperty] private DataTable csvData;

    public ImportCSVData()
    {
        csvData = new DataTable();
        headerProperties = new ObservableCollection<CsvHeaders>();
        unknownProperties = new ObservableCollection<CsvHeaders>();

        errorMessage = "";
    }

    public async Task<bool> PrepFileForCSVImport(FileResult csvFile)
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

            using (var stream = await csvFile.OpenReadAsync())
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

                    // Add columns to DataTable
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

                    if (localSeriesNameColumnIndex == -1)
                    {
                        // Attempt to find "Name" as a fallback
                        for (int i = 0; i < headers.Length; i++)
                        {
                            if (headers[i].Trim().Equals("Name", StringComparison.OrdinalIgnoreCase))
                            {
                                localSeriesNameColumnIndex = i;
                                // Consider logging a warning that "Series.Name" was not found and "Name" is used as fallback.
                                break;
                            }
                        }
                        if (localSeriesNameColumnIndex == -1)
                        {
                            errorMessage = "Column 'Series.Name' or 'Name' not found in CSV headers. Cannot determine unique series count.";
                            // We can still load the data, but SeriesFound might be inaccurate or 0.
                            // Depending on requirements, this could be a return false scenario.
                        }
                    }

                    var uniqueSeriesNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    int rowCount = 0;

                    while (csvReader.Read())
                    {
                        rowCount++;
                        DataRow dataRow = this.CsvData.NewRow();
                        bool rowHasSeriesName = false;
                        string currentSeriesName = null;

                        for (int i = 0; i < headers.Length; i++)
                        {
                            try
                            {
                                var fieldValue = csvReader.GetField(i);
                                dataRow[i] = fieldValue;
                                if (i == localSeriesNameColumnIndex && !string.IsNullOrWhiteSpace(fieldValue))
                                {
                                    currentSeriesName = fieldValue.Trim();
                                    rowHasSeriesName = true;
                                    uniqueSeriesNames.Add(currentSeriesName);
                                }
                            }
                            catch (MissingFieldException) // Handles if a row is shorter than header count
                            {
                                dataRow[i] = DBNull.Value; // Or string.Empty, depending on preference
                            }
                            catch (IndexOutOfRangeException) // Should not happen if GetField(i) is used with headers.Length
                            {
                                dataRow[i] = DBNull.Value;
                            }
                        }
                        this.CsvData.Rows.Add(dataRow);

                    
                    }

                 

                    CsvData.TableName = "BooksToImport";
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
            ErrorHandler.AddError(new Exception("CSV header validation failed.", ex));
            this.CsvData?.Clear(); // Clear data on error
            this.CsvData?.Columns.Clear();
            return false;
        }
        catch (CsvHelperException ex) // Catches CsvHelper specific exceptions
        {
            this.errorMessage = $"Error processing CSV file with CsvHelper: {ex.Message}";
            ErrorHandler.AddError(new Exception("Error processing CSV file with CsvHelper.", ex));
            this.CsvData?.Clear();
            this.CsvData?.Columns.Clear();
            return false;
        }
        catch (Exception ex)
        {
            this.errorMessage = $"Unable to read in CSV file: {ex.Message}";
            ErrorHandler.AddError(new Exception("Unable to read in csv file.", ex));
            this.CsvData?.Clear();
            this.CsvData?.Columns.Clear();
            return false;
        }
    }
}
