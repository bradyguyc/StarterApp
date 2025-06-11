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

            // Add "Ready" column first
            this.CsvData.Columns.Add("Ready", typeof(bool));
            if (this.CsvData.Columns.Contains("Ready")) // Ensure it was added before setting ordinal
            {
                this.CsvData.Columns["Ready"].SetOrdinal(0); // Make it the first column
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
                        dataRow["Ready"] = false;

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
                                    // rowHasSeriesName = true; // This variable was unused
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
                        // Set default values for the new trailing columns
                

                        this.CsvData.Rows.Add(dataRow);
                    }

                    CsvData.TableName = "BooksToImport";
                    // Add additional columns at the end
                   
                    this.CsvData.Columns.Add("OLWork", typeof(string));
                    this.CsvData.Columns.Add("OLEdition", typeof(string));
                    this.CsvData.Columns.Add("OLStatus", typeof(string));
                    this.CsvData.Columns.Add("OLReady", typeof(string));
                    this.CsvData.Columns["OLReady"].SetOrdinal(1); // Make "OLReady" the first column
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
            this.CsvData?.Clear();
            this.CsvData?.Columns.Clear();
            return false;
        }
        catch (CsvHelperException ex)
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
