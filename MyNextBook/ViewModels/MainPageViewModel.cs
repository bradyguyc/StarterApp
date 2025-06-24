using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

using MyNextBook.Helpers;
using MyNextBook.Models;
using MyNextBook.Services;
using MyNextBook.Views;

using OpenLibraryNET.Data;


namespace MyNextBook.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        [ObservableProperty] ObservableCollection<Series> itemsSeries;
        [ObservableProperty] ShowPopUpDetails popupDetails;
        [ObservableProperty] private List<string> idTokenClaims = new();
        [ObservableProperty] private string introText = string.Empty;
        [ObservableProperty] private bool? signInEnabled = true;
        [ObservableProperty] private bool? showWelcome = false;
        [ObservableProperty] private bool? showSeries = false;
        [ObservableProperty] private bool? isRefreshing = false;
        bool ProcessingAppearing = false;
        IOpenLibraryService OLService;
        private readonly ILogger<MainPageViewModel> _logger;

        [ObservableProperty] private bool isSignedIn;
     
        public MainPageViewModel(ILogger<MainPageViewModel> logger)
        {

            _logger = logger;
            //App.Current.UserAppTheme = AppTheme.Dark;
            PopupDetails = new ShowPopUpDetails();
            PopupDetails.IsOpen = false;
          
            //SignInToAppAndOLAsync(); 

        }



        private async Task SignInToAppAndOLAsync()
        {
            //todo: I don't like loading error dictionary here.  Seems like it should be done in constructor. But had performance and timing issues.
            //await ErrorDictionary.LoadErrorsFromFile();
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
            bool credentialAvailable = await StaticHelpers.OLAreCredentialsSetAsync();
            if (credentialAvailable) { 

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
        [RelayCommand] async Task Refresh()
        {
            try
            {
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


    }
}
