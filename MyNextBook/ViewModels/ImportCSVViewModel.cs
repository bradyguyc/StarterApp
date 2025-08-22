using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Data;

using CommonCode.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.Messaging.Messages;
using ImportSeries;
using ImportSeries.Models;
using ImportSeries.Services;
using DevExpress.Maui.DataGrid;
using System.Reflection;

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
        private readonly IPendingTransactionService _transactionService;

        public ImportCSVViewModel(IOpenLibraryService openLibraryService, IPendingTransactionService transactionService)
        {
            try
            {
                IsBusy = false;
                ShowInitial = true;
                ShowImporting = false;
                ShowImport = false;
                ImportProgressText = "Importing CSV";
                WeakReferenceMessenger.Default.Register<ImportProgressMessage>(this, (r, m) =>
                {
                    // Ensure the message is for progress updates and handle it on the UI thread
                    MainThread.BeginInvokeOnMainThread(() =>
                    {
                        Debug.WriteLine($"Progress: {m.Progress} - {m.Text}");
                        ImportProgressValue = m.Progress;
                        ImportProgressText = m.Text;
                        Task.Delay(300).Wait();
                    });
                    
                });

                _transactionService = transactionService;
                iCSVData = new ImportCSVData(_openLibraryService, _transactionService); // changed
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
        [ObservableProperty] private bool isMenuPopupOpen = false;

        [ObservableProperty] private string importInstructions = Constants.ImportInstructions;
        [ObservableProperty] private string importProgressText;
        [ObservableProperty] private double importProgressValue = 0;
        //[ObservableProperty] private bool showImportText;
        [ObservableProperty] private string? fileToImport;
        [ObservableProperty] private bool showInitial;
        [ObservableProperty] private bool showImporting = false;
        [ObservableProperty] private string importText = string.Empty;
        [ObservableProperty] private bool showImport = false;
        [ObservableProperty] private bool showImportProgress = false;
        [ObservableProperty] private ShowPopUpDetails? popupDetails;
        [ObservableProperty] private ObservableCollection<ImportSeriesResults> bookProcesingList = new ObservableCollection<ImportSeriesResults>();
        public ImportCSVData? iCSVData { get; set; }
        #endregion
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
                //ShowImportText = false;
                ImportProgressText = "Importing CSV";
                ShowImportProgress = false;
                ImportProgressValue = 0;
                iCSVData = new ImportCSVData(_openLibraryService, _transactionService); // changed
                PopupDetails = new ShowPopUpDetails
                {
                    IsOpen = false,
                    ErrorCode = string.Empty,
                    ErrorMessage = string.Empty,
                    ErrorReason = string.Empty
                };
                // Initialize non-nullable fields to avoid CS8618

                FileToImport = string.Empty;
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
        [RelayCommand] Task ShowMenu()
        {
            IsMenuPopupOpen = true;
            return Task.CompletedTask;
        }
        [RelayCommand] Task ShowHelp()
        {
            try
            {
                //ShowImportText = !ShowImportText;
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

            try
            {
                // Prefer not to navigate past root; use PopToRoot when possible
                if (Shell.Current?.Navigation?.NavigationStack?.Count > 1)
                {
                    await Shell.Current.Navigation.PopToRootAsync(true);
                }
                else
                {
                    // Absolute route to MainPage tab/content
                    await Shell.Current.GoToAsync("///MainPage", true);
                }
            }
            catch
            {
                // Fallback to absolute navigation without crashing
                await Shell.Current.GoToAsync("///MainPage", true);
            }
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
                    
                    // Yield control to allow the UI to update and show the ActivityIndicator
                    await Task.Yield();

                    FileToImport = "File: " + result.FileName;

                    try
                    { 
                        ShowImportProgress = true;
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
                        ShowInitial = false;
                        ShowImportProgress = false;
                        //ImportText = await CommonCode.Helpers.FileHelpers.ReadTextFile("introtext.txt");
                        //ShowImportText = true;
                    }
                    catch (Exception ex)
                    {
                        PopupDetails = new ShowPopUpDetails
                        {
                            IsOpen = true,
                            ErrorMessage = ex.Message,
                            ErrorCode = "ERR-000"
                        };
                        ShowInitial = true;
                        ShowImportProgress = false;
                        ErrorHandler.AddError(ex);
                        OnPropertyChanged(nameof(PopupDetails));
                    }
                    finally
                    {
                        // Ensure IsBusy is set to false regardless of success or failure
                        ShowInitial = false;
                        IsBusy = false;
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
        private async Task AddToLibrary()
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
            // Navigate safely back to the root/main page
            try
            {
                if (Shell.Current?.Navigation?.NavigationStack?.Count > 1)
                {
                    await Shell.Current.Navigation.PopToRootAsync(true);
                }
                else
                {
                    await Shell.Current.GoToAsync("///MainPage", true);
                }
            }
            catch
            {
                await Shell.Current.GoToAsync("///MainPage", true);
            }
        }
        #endregion
        #endregion

        /*

        [RelayCommand]
        private async Task Back()
        {
            ShowImport = false;
            ShowImporting = false;
            ShowInitial = true;

            try
            {
                // Pop a page if possible, otherwise go to the main tab
                if (Shell.Current?.Navigation?.NavigationStack?.Count > 1)
                {
                    await Shell.Current.Navigation.PopAsync(true);
                }
                else
                {
                    await Shell.Current.GoToAsync("///MainPage", true);
                }
            }
            catch
            {
                await Shell.Current.GoToAsync("///MainPage", true);
            }
        }
        */
        [RelayCommand] async Task ImportReadyItems()
        {
            try
            {
                ShowImport = true;
           
                ShowInitial = false;
             //   ShowImportText = false;
                ImportProgressText = "Importing CSV";
                ShowImportProgress = false;
                ImportProgressValue = 0;
                IsMenuPopupOpen = false;
                await iCSVData.CreateOpenLibraryListsForReadySeries();
                return;
            }
            catch (Exception ex)
            {
                PopupDetails.IsOpen = true;
                PopupDetails.ErrorMessage = ex.Message;
                PopupDetails.ErrorCode = "ERR-000";
                ErrorHandler.AddLog(ex.Message);
                return;
            }
        }

        [RelayCommand]
        private async Task SaveAndReturnLater()
        {
            try
            {
                // Save current state and return to main page
                await Shell.Current.GoToAsync("///MainPage", true);
            }
            catch (Exception ex)
            {
                PopupDetails = new ShowPopUpDetails
                {
                    IsOpen = true,
                    ErrorMessage = ex.Message,
                    ErrorCode = "ERR-001"
                };
                ErrorHandler.AddError(ex);
            }
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

        [RelayCommand]
        private async Task SearchForBook(object param)
        {
            try
            {
                DataRowView rowView = null;

                switch (param)
                {
                    case DataRowView drv:
                        rowView = drv; break;
                    case DataRow dataRow:
                        rowView = dataRow.RowState != DataRowState.Deleted ? dataRow.Table.DefaultView[dataRow.Table.Rows.IndexOf(dataRow)] : null; break;
                    case CellData cellData:
                        // DevExpress CellData exposes the underlying value in cellData.Value; need row item via grid
                        // Attempt to get private fields via reflection (implementation detail) if necessary
                        var rowHandleProp = cellData.GetType().GetProperty("RowHandle", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (rowHandleProp != null && rowHandleProp.GetValue(cellData) is int rh && rh >= 0)
                        {
                            // Try to locate an associated DataGridView from open pages (simplest: not available here) -> fallback unsupported
                        }
                        // If Value already is DataRowView just use it
                        if (cellData.Value is DataRowView maybeDrv)
                            rowView = maybeDrv;
                        break;
                }

                if (rowView == null)
                {
                    PopupDetails = new ShowPopUpDetails
                    {
                        IsOpen = true,
                        ErrorMessage = "Row data unavailable for search.",
                        ErrorCode = "ERR-ROW"
                    };
                    return;
                }

                var row = rowView.Row;
                string title = row.Table.Columns.Contains("Book.Title") ? row["Book.Title"]?.ToString() ?? string.Empty : string.Empty;
                string author = row.Table.Columns.Contains("Book.Author") ? row["Book.Author"]?.ToString() ?? string.Empty : string.Empty;
                string isbn10 = row.Table.Columns.Contains("Book.ISBN_10") ? row["Book.ISBN_10"]?.ToString() ?? string.Empty : string.Empty;
                string isbn13 = row.Table.Columns.Contains("Book.ISBN_13") ? row["Book.ISBN_13"]?.ToString() ?? string.Empty : string.Empty;

                Debug.WriteLine($"SearchForBook invoked: Title='{title}', Author='{author}', ISBN10='{isbn10}', ISBN13='{isbn13}'");

                PopupDetails = new ShowPopUpDetails
                {
                    IsOpen = true,
                    ErrorMessage = $"Searching for: {title}{(string.IsNullOrWhiteSpace(author) ? string.Empty : " by " + author)}",
                    ErrorCode = string.Empty
                };
            }
            catch (Exception ex)
            {
                PopupDetails = new ShowPopUpDetails
                {
                    IsOpen = true,
                    ErrorMessage = $"Search failed: {ex.Message}",
                    ErrorCode = "ERR-SEARCH"
                };
                ErrorHandler.AddError(ex);
            }
        }
    }
}