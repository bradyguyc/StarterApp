using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using CommonCode.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using ImportSeries;
using ImportSeries.Models;

using DevExpress.Maui.DataGrid;

using MyNextBook.Helpers;
using MyNextBook.Models;
using MyNextBook.Services;
using MyNextBook.Views;

using Newtonsoft.Json;

namespace MyNextBook.ViewModels
{

    public partial class ImportCSVViewModel : ObservableObject
    {
        private readonly IOpenLibraryService? _openLibraryService;

        public ImportCSVViewModel(IOpenLibraryService openLibraryService)
        {
            try
            {
                IsBusy = false;
                ShowInitial = true;
                ShowImporting = false;
                ShowImport = false;
                ShowImportText = false;
                ImportProgressText = "Importing CSV";
                WeakReferenceMessenger.Default.Register<ImportProgressMessage>(this, (r, m) =>
                {
                    // Ensure the message is for progress updates and handle it on the UI thread
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        ImportProgressValue = m.Value;
                        ImportProgressText = m.Text;
                    });
                    
                });

                iCSVData = new ImportCSVData();
                _openLibraryService = openLibraryService;
                PopupDetails = new ShowPopUpDetails
                {
                    IsOpen = false,
                    ErrorCode = string.Empty,
                    ErrorMessage = string.Empty,
                    ErrorReason = string.Empty
                };
                // Initialize non-nullable fields to avoid CS8618

                FileToImport = string.Empty;

            }
            catch (Exception ex)
            {
                PopupDetails.IsOpen = true;
                PopupDetails.ErrorMessage = ex.Message;
                PopupDetails.ErrorCode = "ERR-000";
                ErrorHandler.AddLog(ex.Message);
            }
        }

        #region Standard Properties
        [ObservableProperty] private bool isBusy = false;
        [ObservableProperty] private ShowPopUpDetails popUpDetails;
        #endregion

        [ObservableProperty] private string importInstructions = Constants.ImportInstructions;
        [ObservableProperty] private string importProgressText;
        [ObservableProperty] private double importProgressValue = 0;
        [ObservableProperty] private bool showImportText;
        [ObservableProperty] private string? fileToImport;
        [ObservableProperty] private bool showInitial;
        [ObservableProperty] private bool showImporting = false;
        [ObservableProperty] private string importText = string.Empty;
        [ObservableProperty] private bool showImport = false;
        [ObservableProperty] private ShowPopUpDetails? popupDetails;
        [ObservableProperty] private ObservableCollection<ImportSeriesResults> bookProcesingList = new ObservableCollection<ImportSeriesResults>();
        public ImportCSVData? iCSVData { get; set; }
        [RelayCommand]
        Task Appearing()
        {
            try
            {
                IsBusy = false;
                ShowInitial = true;
                OnPropertyChanged(nameof(ShowInitial));
                ShowImporting = false;
                ShowImport = false;
                ShowImportText = false;
                ImportProgressText = "Importing CSV";
                ImportProgressValue = 0;
                iCSVData = new ImportCSVData();
                PopupDetails = new ShowPopUpDetails
                {
                    IsOpen = false,
                    ErrorCode = string.Empty,
                    ErrorMessage = string.Empty,
                    ErrorReason = string.Empty
                };
                // Initialize non-nullable fields to avoid CS8618

                FileToImport = string.Empty;
                Task.Delay(100);
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                PopupDetails.IsOpen = true;
                PopupDetails.ErrorMessage = ex.Message;
                PopupDetails.ErrorCode = "ERR-000";
                ErrorHandler.AddLog(ex.Message);
                return Task.CompletedTask;
            }
        }
      
        #region import csv file
        [RelayCommand]
        private async Task CancelImport(Object param)
        {
            ShowImport = false;
            ShowImporting = false;
            ShowInitial = true;
            await Shell.Current.GoToAsync($"..\\..");
        }

        #region Import Step 1
        [RelayCommand]
        private async Task PerformImport(Object param)
        {
            try
            {
                ShowImporting = false;
                ShowInitial = false;
                var options = new PickOptions
                {
                    FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                            { DevicePlatform.iOS, new[] { "com.microsoft.excel.xls", "public.item" } },
                            { DevicePlatform.Android, new[] { "*/*" } },
                            // Add other platforms here if needed
                        }),
                    PickerTitle = "Please select an Excel file"
                };
                var result = await FilePicker.Default.PickAsync(options);
                
                if (result != null)
                {
                    IsBusy = true;
                    Task.Delay(100);
                    // Yield control to allow the UI to update and show the ActivityIndicator
                    await Task.Yield();

                    FileToImport = "File: " + result.FileName;

                    try
                    { 
                        using var stream = await result.OpenReadAsync();
                        await iCSVData.Import(stream);
                        
                        OnPropertyChanged("iCSVData");
                        
                        // Small delay to allow UI to update with data first
                        await Task.Delay(100);
                        
                        DataGridView gridView = param as DataGridView;
                        if (gridView != null && gridView.ItemsSource != null)
                        {
                            gridView.GroupBy("Series.Name");
                          
                        }
                        ShowImporting = true;
                        //ImportText = await CommonCode.Helpers.FileHelpers.ReadTextFile("introtext.txt");
                        //ShowImportText = true;
                    }
                    catch (Exception ex)
                    {
                        PopupDetails = new ShowPopUpDetails
                        {
                            IsOpen = true,
                            ErrorMessage = ex.Message,
                            ErrorCode = "ERR-003"
                        };
                        ShowInitial = true;
                        ErrorHandler.AddError(ex);
                        OnPropertyChanged(nameof(PopupDetails));
                    }
                    finally
                    {
                        // Ensure IsBusy is set to false regardless of success or failure
                        ShowInitial = true;
                        IsBusy = false;
                        Task.Delay(100);
                    }
                    //Debug.WriteLine("Sereis:" + iCSVData.SeriesFound + " books:" + iCSVData.BooksFound);
                }
                else
                {
                    Debug.WriteLine("No file selected - user cancelled");
                    ShowInitial = true;
                    return; // Don't navigate away, just return to initial state
                }
            }
            catch (Exception ex)
            {
                // The user canceled or something went wrong
                // It's good practice to also set IsBusy = false here if an unexpected error occurs
                // before the try/finally block for IsBusy is reached, though in this specific
                // structure, the main concern is the try/finally around PrepFileForCSVImport.
                IsBusy = false;
                ErrorHandler.AddError(ex);
            }
        }

        #endregion

        #region Import Step 2
        /*
        // 1. Define the mapping as a JSON string (could also be loaded from a file/resource)
        private const string ColumnMappingJson = @"{
            ""Series.Name"": ""seriesName"",
            ""Book.Title"": ""bookTitle"",
            ""Book.Author"": ""author"",
            ""Book.PublishedDate"": ""publishedDate"",
            ""Book.ISBN_10"": ""isbn10"",
            ""Book.ISBN_13"": ""isbn13"",
            ""Book.OLID"": ""olid""
        }";

        // 2. Deserialize to a dictionary
        private static readonly Dictionary<string, string> ColumnToVariableMap =
            JsonConvert.DeserializeObject<Dictionary<string, string>>(ColumnMappingJson);
        */
        [RelayCommand]
        private async Task GetDetails(Object param)
        {
            try
            {

                // AllBooks = new ObservableCollection<Book>(iCSVData.csvData.SelectMany(series => series.Books));
                ShowImport = false;

                ShowImporting = true;

                //BooksFilledIn = 0;
                int booksNotFound = 0;



                await Task.Run(async () =>
                {

                    IsBusy = true;
                    // Example: Loop through each row in the DataTable (assuming iCSVData.csvData is a DataTable)
                    if (iCSVData.CsvData != null)
                    {
                        //bool fillSuccessfully = await iCSVData.FillInSeriesDataViaAI(this.iCSVData);
                        /*
                        if (!fillSuccessfully)
                        {
                            PopupDetails.IsOpen = true;
                            PopupDetails.ErrorMessage = "Failed to fill in series data via AI.";
                            PopupDetails.ErrorCode = "ERR-004";
                        }
                        */
                        // Access data by column name or index, e.g.:
                        //var value = row["ColumnName"]; // or row[0]
                        // TODO: Process each row as needed
                       
                        /*foreach (System.Data.DataRow row in iCSVData.CsvData.Rows)
                        {
                            // Map row columns to variables
                            MapRowToVariables(row, out string seriesName, out string bookTitle, out string author, out string publishedDate, out string isbn10, out string isbn13, out string olid);

                            // Now call SearchForWorks with mapped variables
                            var results = await _openLibraryService.SearchForWorks(
                                 bookTitle,
                                 author,
                                 publishedDate,
                                 isbn10,
                                 isbn13,
                                 olid);


                            if (results != null)
                            {

                            }
                        }
                        */

                    }

                });
            }
            catch (Exception ex)
            {
                //throw error here
                ErrorHandler.AddError(ex);
            }
        }
        #endregion

        #region Import Step 3
        [RelayCommand]
        private void AddToLibrary()
        {
            /*
            foreach (var book in AllBooks)
            {
                var matchingBook = MySeriesPageViewModel.currentData.series
                    .SelectMany(s => s.Books)
                    .FirstOrDefault(b => b.Equals(book));

                if (matchingBook != null)
                {
                    // Update the book with the matching book's details
                    book.SetUserBookStatus(matchingBook.GetUserBookStatus());
                }
            }
            var groupedBooks = AllBooks.GroupBy(b => b.SeriesName);

            foreach (var group in groupedBooks)
            {
                var seriesName = group.Key;

                var books = new ObservableCollection<Book>(group.ToList());

                var newSeries = new Series(seriesName, books, "", "", "");
                newSeries.OpenImageUrl = books.FirstOrDefault(b => !string.IsNullOrEmpty(b.OpenImageUrl))?.OpenImageUrl;
                

                
                int i= 1;
                newSeries.Books.OrderBy(b => b.PublishDate).ForEach(b => b.SeriesBookSortOrder = i++);
                MySeriesPageViewModel.currentData.series.Add(newSeries);
            }
            */
            Shell.Current.GoToAsync("../..");
        }
        #endregion
        #endregion


        [RelayCommand]
        void Back()
        {
            ShowImport = false;
            ShowImporting = false;
            ShowInitial = true;
            Shell.Current.GoToAsync("../..");

        }

        // 3. Usage in row
        //
        //
        /*
        private void MapRowToVariables(System.Data.DataRow row, out string? seriesName, out string? bookTitle, out string? author, out string? publishedDate, out string? isbn10, out string? isbn13, out string? olid)
        {
            seriesName = iCSVData.columnHeaderMap.TryGetValue("Series.Name", out string seriesNameField) && !string.IsNullOrEmpty(seriesNameField) && row.Table.Columns.Contains(seriesNameField)
                ? row[seriesNameField]?.ToString() ?? string.Empty
                : string.Empty;
            bookTitle = iCSVData.columnHeaderMap.TryGetValue("Book.BookTitle", out string bookTitleField) && !string.IsNullOrEmpty(bookTitleField) && row.Table.Columns.Contains(bookTitleField)
                ? row[bookTitleField]?.ToString() ?? string.Empty
                : string.Empty;
            author = iCSVData.columnHeaderMap.TryGetValue("Book.Author", out string authorField) && !string.IsNullOrEmpty(authorField) && row.Table.Columns.Contains(authorField)
                ? row[authorField]?.ToString() ?? string.Empty
                : string.Empty;
            publishedDate = iCSVData.columnHeaderMap.TryGetValue("Book.PublishedDate", out string publishedDateField) && !string.IsNullOrEmpty(publishedDateField) && row.Table.Columns.Contains(publishedDateField)
                ? row[publishedDateField]?.ToString() ?? string.Empty
                : string.Empty;
            isbn10 = iCSVData.columnHeaderMap.TryGetValue("Book.ISBN_10", out string isbn10Field) && !string.IsNullOrEmpty(isbn10Field) && row.Table.Columns.Contains(isbn10Field)
                ? row[isbn10Field]?.ToString() ?? string.Empty
                : string.Empty;
            isbn13 = iCSVData.columnHeaderMap.TryGetValue("Book.ISBN_13", out string isbn13Field) && !string.IsNullOrEmpty(isbn13Field) && row.Table.Columns.Contains(isbn13Field)
                ? row[isbn13Field]?.ToString() ?? string.Empty
                : string.Empty;
            olid = iCSVData.columnHeaderMap.TryGetValue("Book.OLID", out string olidField) && !string.IsNullOrEmpty(olidField) && row.Table.Columns.Contains(olidField)
                ? row[olidField]?.ToString() ?? string.Empty
                : string.Empty;
        }
        */
        // 4. Generalized dynamic mapping (optional)
        /*
        private Dictionary<string, string> MapRowToDictionary(System.Data.DataRow row)
        {
            var result = new Dictionary<string, string>();
            foreach (var kvp in ColumnToVariableMap)
            {
                if (row.Table.Columns.Contains(kvp.Key))
                {
                    result[kvp.Value] = row[kvp.Key]?.ToString();
                }
            }
            return result;
        }
        */

    }
}