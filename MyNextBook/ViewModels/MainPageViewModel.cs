using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;


using CommonCode.Helpers;
using CommonCode.Models;
using CommonCode.MSALClient;

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;

using DevExpress.Maui.Editors;

using ImportSeries;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

using MyNextBook.Helpers;
using MyNextBook.Services;
using MyNextBook.Views;

using OpenLibraryNET.Data;

// Use alias to distinguish between the two Series types
using ImportSeriesSeries = ImportSeries.Models.Series;


namespace MyNextBook.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        [ObservableProperty] ObservableCollection<ImportSeries.Models.Series> itemsSeries;
        [ObservableProperty] ShowPopUpDetails popupDetails;
        [ObservableProperty] private List<string> idTokenClaims = new();
        [ObservableProperty] private string introText = string.Empty;
        [ObservableProperty] private bool? signInEnabled = true;
        [ObservableProperty] private bool? showWelcome = false;
        [ObservableProperty] private bool? showSeries = false;
        [ObservableProperty] private bool? isRefreshing = false;

        private readonly ILogger<MainPageViewModel> _logger;
        [ObservableProperty] private ImportSeries.Models.Series? selectedSeries;
        partial void OnSelectedSeriesChanged(ImportSeries.Models.Series oldValue, ImportSeries.Models.Series? newValue)
        {
            System.Diagnostics.Debug.WriteLine($"Selected series changed from: {oldValue?.SeriesData?.Name ?? "null"} to: {newValue?.SeriesData?.Name ?? "null"}");
        }

        [ObservableProperty] private bool isSignedIn;

        bool ProcessingAppearing = false;
        IOpenLibraryService OLService;


        public MainPageViewModel(ILogger<MainPageViewModel> logger)
        {

            _logger = logger;
            //App.Current.UserAppTheme = AppTheme.Dark;
            PopupDetails = new ShowPopUpDetails();
            PopupDetails.IsOpen = false;
            WeakReferenceMessenger.Default.Register<StatusUpdateMessage>(this, HandleStatusUpdate);

            //SignInToAppAndOLAsync(); 

        }
     
        private void HandleStatusUpdate(object recipient, StatusUpdateMessage message)
        {
            Debug.WriteLine($"Received status update for '{message.WorkData.Title}' '{message.WorkData.ID}' to '{message.NewStatus}'");

            var workToUpdate = ItemsSeries?
                .SelectMany(s => s.works)
                .FirstOrDefault(w => w.ID == message.WorkData.ID);

            if (workToUpdate != null)
            {
                workToUpdate.Status = message.NewStatus;
            }
            //Task.Delay(200)

            // Debug: Loop through Works and print out key and status
            if (SelectedSeries?.Works != null)
            {
                Debug.WriteLine($"=== Works in {SelectedSeries.SeriesData?.Name ?? "Unknown Series"} ===");
                foreach (var work in SelectedSeries.Works)
                {
                    Debug.WriteLine($"Key: {work.Key}, Status: {work.Status}");
                }
                Debug.WriteLine($"=== End of Works List ===");
            }

            SelectedSeries.UserBooksRead = SelectedSeries.Works.Count(w => w.Status == "Read");
            Debug.WriteLine($"MainPageViewModel: HandleStatusUpdate Key: {message.WorkData.ID}, Status: {message.NewStatus} BookCount: {SelectedSeries.UserBooksRead}");
            OnPropertyChanged(nameof(SelectedSeries));
            OnPropertyChanged(nameof(ItemsSeries));
            OLService.OLSetStatus(message.WorkData.ID, message.NewStatus).ConfigureAwait(false);
            // TODO: Update OpenLibrary here
            // await OLService.UpdateReadingStatus(message.WorkData, message.NewStatus);
        }

        private async Task SignInToAppAndOLAsync()
        {
            //todo: I don't like loading error dictionary here.  Seems like it should be done in constructor. But had performance and timing issues.
            await ErrorDictionary.LoadErrorsFromFile(); // this is here in case an error happens before everything is loaded and logged in.
                                                        //todo double check that this needs to run on main thread.  I think it does.
                                                        // await MainThread.InvokeOnMainThreadAsync(async () =>
                                                        //{
            IsSignedIn = await SignIn();


            //});
            if (IsSignedIn == false)
            {
                await Shell.Current.GoToAsync("WelcomeScreen");

            }
            if (await SignInOL())
            {
                await Refresh();
            }



        }

        [RelayCommand]
        async Task Appearing()
        {
            if (!ProcessingAppearing)
            {
                ProcessingAppearing = true;
                await SignInToAppAndOLAsync();
                ProcessingAppearing = false;
            }
        }
        /*
        [RelayCommand]
        async Task GoToSettingsPage()
        {
            await Shell.Current.GoToAsync("SettingsPage");

        }
        */
        async Task<bool> SignInOL()
        {
            try
            {
                bool credentialAvailable = await StaticHelpers.OLAreCredentialsSetAsync();
                if (credentialAvailable)
                {

                    string OLUserName = await SecureStorage.Default.GetAsync(Constants.OpenLibraryUsernameKey);
                    string OLPassword = await SecureStorage.Default.GetAsync(Constants.OpenLibraryPasswordKey);

                    OLService.SetUsernamePassword(OLUserName, OLPassword);
                    if (false == await OLService.Login())
                    {
                        PopupDetails.IsOpen = true;
                        PopupDetails.ErrorMessage = "Could not sign in to OpenLibrary. Please check your credentials and/or network.";
                        PopupDetails.ErrorCode = "ERR-002";

                        return false;
                    }
                    return true;
                }
                else
                {
                    await Shell.Current.GoToAsync("SettingsPage");
                }
                return false;
            }
            catch (Exception ex)
            {
                PopupDetails.IsOpen = true;
                PopupDetails.ErrorMessage = $"Could not sign in to OpenLibrary. Please check your credentials and/or network.\n{ex.Message}";
                PopupDetails.ErrorCode = "OL-003";
                return false;
            }
        }


        [RelayCommand]
        public async Task<bool> SignIn()
        {
            SignInEnabled = false;
            try
            {
                string token = null;
                try
                {
                    // This method handles both silent and interactive flows.
                    token = await PublicClientSingleton.Instance.AcquireTokenSilentAsync();
                    IsSignedIn = true;

                    if (OLService == null)
                    {
                        OLService = MauiProgram.Services.GetService<IOpenLibraryService>();
                    }
                    return true;
                }
                catch (Exception ex) when (ex.Message.Contains("Username and/or Password not set"))
                {

                    PopupDetails.IsOpen = true;
                    PopupDetails.ErrorMessage = ex.Message;
                    PopupDetails.ErrorCode = "ERR-001";
                    IsSignedIn = false;
                    return false;
                }
                catch (Exception ex)
                {
                    PopupDetails.IsOpen = true;
                    PopupDetails.ErrorMessage = ex.Message;
                    PopupDetails.ErrorCode = "ERR-001";
                    IsSignedIn = false;
                    return false;
                }


            }
            finally
            {
                SignInEnabled = true;
            }
        }
        [RelayCommand]
        async Task Refresh()
        {
            try
            {
                IsRefreshing = true; // start spinner
                await ShowSyncingToast();
                if (OLService == null)
                    OLService = MauiProgram.Services.GetService<IOpenLibraryService>();


                ItemsSeries = await OLService.GetSeries();

            }
            catch (Exception ex)
            {
                SignInEnabled = true;
                PopupDetails.IsOpen = true;
                PopupDetails.ErrorMessage = ex.Message;
                PopupDetails.ErrorCode = "ERR-000 ";
            }
            finally
            {
                IsRefreshing = false; // stop spinner
            }

        }
        public async Task ShowSyncingToast()
        {
            try
            {
                CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
                string text = "Syncing with OpenLibrary";
                ToastDuration duration = ToastDuration.Short;
                double fontSize = 14;

                var toast = Toast.Make(text, duration, fontSize);
                await toast.Show(cancellationTokenSource.Token);
            }
            catch (Exception ex)
            {
                _logger.LogError("error:" + ex.Message);
                Debug.WriteLine("error:" + ex.Message);
            }
        }
        /*
        [RelayCommand]  
        public async Task SetReadingStatusAsync(object parameter)
        {   
            try
            {
                if (parameter is ImportSeries.Models.OlWorkDataExt workData)
                {
                    if (SelectedSeries != null && SelectedSeries.SeriesData != null)
                    {
                        _logger.LogInformation($"Setting reading status for work {workData.Title} in series {SelectedSeries.SeriesData.Name} to {workData.Status}");
                        
                        // Update the status in OpenLibrary here
                        // await OLService.UpdateReadingStatus(workData, workData.Status);
                    }
                }
                else
                {
                    PopupDetails.IsOpen = true;
                    PopupDetails.ErrorMessage = $"Invalid parameter type. Expected OlWorkDataExt, got {parameter?.GetType().Name ?? "null"}.";
                    PopupDetails.ErrorCode = "ERR-005";
                }
            }
            catch (Exception ex)
            {
                PopupDetails.IsOpen = true;
                PopupDetails.ErrorMessage = ex.Message;
                PopupDetails.ErrorCode = "ERR-004";
            }
        }
            */


    }
}
