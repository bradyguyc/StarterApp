using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using DevExpress.Maui.DataGrid;

using MyNextBook.Helpers;
using MyNextBook.Models;
using MyNextBook.Views;

namespace MyNextBook.ViewModels
{

    public partial class ImportCSVViewModel : ObservableObject
    {
     
        public ImportCSVViewModel()
        {
            try
            {
                IsBusy = false;
                FinishedFillingIn = false;

                ShowInitial = true;
                ShowImporting = false;
                ShowErrorPopup = false;
                ShowImport = false;
                FillInData = true;
                iCSVData = new ImportCSVData();
                //  PerformImport(null);

            }
            catch (Exception ex)
            {
                ErrorHandler.AddLog(ex.Message);
            }
        }

        #region Standard Properties
        [ObservableProperty] private bool isBusy = false;
        [ObservableProperty] private bool showErrorPopup;
        [ObservableProperty] private string errorCode;
        [ObservableProperty] private string errorMessage;
        [ObservableProperty] private string errorReason;
        #endregion

        [ObservableProperty] private string matchedText = Constants.matchedText;
        [ObservableProperty] private string importInstructions = Constants.ImportInstructions;
        [ObservableProperty] private decimal importProgress = 0;
        [ObservableProperty] private bool showImport = false;
        [ObservableProperty] private ObservableCollection<Book> allBooks;
        [ObservableProperty] private string fillinMissingDataText = Constants.FillinMissingDataText;
        [ObservableProperty] private bool fillInData = true;
        [ObservableProperty] private string fillInStatusText;
        [ObservableProperty] private string fileToImport;
        //[ObservableProperty] private int booksFilledIn;
        [ObservableProperty] private bool finishedFillingIn = false;
        [ObservableProperty] private bool showInitial = false;
        [ObservableProperty] private bool showImporting = false;
        [ObservableProperty] private ObservableCollection<ImportSeriesResults> bookProcesingList = new ObservableCollection<ImportSeriesResults>();

        public ImportCSVData iCSVData { get; set; }

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
                    FileToImport = "File: " + result.FileName;

                    await iCSVData.PrepFileForCSVImport(result);
                    OnPropertyChanged("iCSVData");
                    IsBusy = false;
                    ShowImport = true;

                    //Debug.WriteLine("Sereis:" + iCSVData.SeriesFound + " books:" + iCSVData.BooksFound);
                }
                else
                {
                    Shell.Current.GoToAsync($"..");
                }
            }
            catch (Exception ex)
            {
                // The user canceled or something went wrong
                //todo: throw error here
                ErrorHandler.AddError(ex);
            }
        }
        #endregion

        #region Import Step 2
        [RelayCommand]
        private async Task PerformImportInsert(Object param)
        {
            try
            {

               // AllBooks = new ObservableCollection<Book>(iCSVData.csvData.SelectMany(series => series.Books));
                ShowImport = false;

                ShowImporting = true;

                //BooksFilledIn = 0;
                int booksNotFound = 0;
                //FillInStatusText = "Filling in missing data for " + iCSVData.BooksFound + " books.";
              /*
                await Task.Run(async () =>
                {
                    if (FillInData)
                    {
                        GoogleBookSearch googleMatch = new GoogleBookSearch();
                        FilledInStats stats;
                        IsBusy = true;
                        foreach (Series s in iCSVData.csvData)
                        {
                            s.id = Guid.NewGuid().ToString();
                            s.Books.ForEach(
                                book =>
                                {
                                     book.sysMynbID = Guid.NewGuid();
                                });
                            stats = await googleMatch.PerformGoogleRefresh(s, true);
                            //booksNotFound += stats.BooksNotFound;
                            //BooksFilledIn += s.Books.Count;
                            ImportProgress = (decimal)s.Books.Count / (decimal)iCSVData.BooksFound;
                            //FillInStatusText = BooksFilledIn + " of " + iCSVData.BooksFound + " filled in.  Books not found: " + booksNotFound;
                            BookProcesingList.Add(new ImportSeriesResults { BooksMatched = s.Books.Count - stats.BooksNotFound, BooksNotFound = stats.BooksNotFound, SeriesName = s.Name });
                        }
                         //await MySeriesPageViewModel.currentData.OLHClient.UpdateBooksWithOLDataAsync(MySeriesPageViewModel.currentData.series);
                        IsBusy = false;
                    }
                });
              */
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
            Shell.Current.GoToAsync("../..");
            ShowInitial = true;
        }
       
     
    }
}