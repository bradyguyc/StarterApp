﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using CommonCode.Models;

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
                

                ShowInitial = true;
                ShowImporting = false;
                ShowErrorPopup = false;
                ShowImport = false;
                
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

        //[ObservableProperty] private string matchedText = Constants.matchedText;
        [ObservableProperty] private string importInstructions = Constants.ImportInstructions;
        //[ObservableProperty] private decimal importProgress = 0;
        //[ObservableProperty] private bool showImport = false;
        //[ObservableProperty] private ObservableCollection<Book> allBooks;
        //[ObservableProperty] private string fillinMissingDataText = Constants.FillinMissingDataText;
        //[ObservableProperty] private bool fillInData = true;
        //[ObservableProperty] private string fillInStatusText;
        [ObservableProperty] private string fileToImport;
        //[ObservableProperty] private int booksFilledIn;
        //[ObservableProperty] private bool finishedFillingIn = false;
        [ObservableProperty] private bool showInitial = false;
        [ObservableProperty] private bool showImporting = false;
        
        [ObservableProperty] private bool showImport = false;
        [ObservableProperty] private ShowPopUpDetails popupDetails;
        [ObservableProperty] private ObservableCollection<ImportSeriesResults> bookProcesingList = new ObservableCollection<ImportSeriesResults>();

        public ImportCSVData iCSVData { get; set; }
        [RelayCommand] private async Task Loaded()
        {
            ShowImport = false;
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
                    // Yield control to allow the UI to update and show the ActivityIndicator
                    await Task.Yield(); 
                    
                    FileToImport = "File: " + result.FileName;

                    try
                    {
                        await iCSVData.PrepFileForCSVImport(result);
                        DataGridView gridView = param as DataGridView;
                        if (gridView != null) // Check if the cast is successful
                        {
                            gridView.GroupBy("Series.Name");
                          
                        }


                        OnPropertyChanged("iCSVData");
                        ShowImporting = true;
                    } catch (Exception ex)
                    {
                        PopupDetails = new ShowPopUpDetails
                        {
                            IsOpen = true,
                            ErrorMessage = ex.Message,
                            ErrorCode = "ERR-003"
                        };

                        OnPropertyChanged(nameof(PopupDetails));
                    }
                    finally
                    {
                        // Ensure IsBusy is set to false regardless of success or failure
                        IsBusy = false; 
                    }
                    //Debug.WriteLine("Sereis:" + iCSVData.SeriesFound + " books:" + iCSVData.BooksFound);
                }
                else
                {
                    await Shell.Current.GoToAsync($"..");
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
                    if (iCSVData.csvData != null)
                    {
                        foreach (System.Data.DataRow row in iCSVData.csvData.Rows)
                        {
                            // Access data by column name or index, e.g.:
                            var value = row["ColumnName"]; // or row[0]
                            // TODO: Process each row as needed
                        }
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
            Shell.Current.GoToAsync("../..");
            ShowInitial = true;
        }
       
     
    }
}