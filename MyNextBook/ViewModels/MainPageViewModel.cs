using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using CommonCode.Helpers;
using CommonCode.Models;
using CommonCode.MSALClient;

using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

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
        IOpenLibraryService OLService;
        private readonly ILogger<MainPageViewModel> _logger;

        [ObservableProperty] private bool isSignedIn;

        public MainPageViewModel(ILogger<MainPageViewModel> logger)
        {
          
            _logger = logger;
            //App.Current.UserAppTheme = AppTheme.Dark;
            PopupDetails = new ShowPopUpDetails();
            PopupDetails.IsOpen = false;
            InitializeAsync();
     
        }



        private async Task InitializeAsync()
        {
            //todo: I don't like loading error dictionary here.  Seems like it should be done in constructor. But had performance and timing issues.
            await ErrorDictionary.LoadErrorsFromFile();
            //todo double check that this needs to run on main thread.  I think it does.
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {

            
                var cachedUserAccount =
                    await PublicClientSingleton.Instance.MSALClientHelper.FetchSignedInUserFromCache();
                IsSignedIn = cachedUserAccount != null;
            });
            if (IsSignedIn == false)
            {
                await Shell.Current.GoToAsync("WelcomeScreen");
             
           

            }
            else
            {
                
                bool credentialAvailable = await StaticHelpers.OLAreCredentialsSetAsync();

                if (!credentialAvailable)
                {
                    //Application.Current.Windows[0].Page = new MySeriesPage();
                    await Shell.Current.GoToAsync("SettingsPage");

                }
              
            }
            //SignIn();

        }

        [RelayCommand]
        async Task Appearing()
        {
            var cachedUserAccount =
                   await PublicClientSingleton.Instance.MSALClientHelper.FetchSignedInUserFromCache();
            IsSignedIn = cachedUserAccount != null;

            if (!IsSignedIn)
            {
                await Shell.Current.GoToAsync("WelcomeScreen");

            }
            bool credentialAvailable = await StaticHelpers.OLAreCredentialsSetAsync();
            if (IsSignedIn && credentialAvailable)
            {
                ShowSeries = true;
                EnsureSeriesAreLoaded();
            }
            else
            {
                ShowWelcome = true;
                PopupDetails.IsOpen = true;
                PopupDetails.ErrorCode = "ERR-005";
                return;
            }
        }
        [RelayCommand]
        async void GoToSettingsPage()
        {
            await Shell.Current.GoToAsync("SettingsPage");

        }
        [RelayCommand]
        public async Task SignIn()
        {
            SignInEnabled = false;
            try
            {
                string token = null;
                try
                {
                    // This method handles both silent and interactive flows.
                    token = await PublicClientSingleton.Instance.AcquireTokenSilentAsync();
                }
                catch (Exception ex)
                {
                    PopupDetails.IsOpen = true;
                    PopupDetails.ErrorMessage = ex.Message;
                    PopupDetails.ErrorCode = "ERR-001";
                    IsSignedIn = false;
                    return;
                }

                if (!string.IsNullOrEmpty(token))
                {
                    IsSignedIn = true;
                }
                else
                {
                    IsSignedIn = false;
                    if (!PopupDetails.IsOpen)
                    {
                        PopupDetails.IsOpen = true;
                        PopupDetails.ErrorCode = "ERR-002 ";
                    }
                }

                if (IsSignedIn)
                {
                    bool credentialAvailable = await StaticHelpers.OLAreCredentialsSetAsync();
                    if (credentialAvailable)
                    {
                        ShowWelcome = false;
                        ShowSeries = true;
                    }
                    else
                    {
                        await Shell.Current.GoToAsync("SettingsPage");
                    }
                }
            }
            finally
            {
                SignInEnabled = true;
            }
        }
        partial void OnIsSignedInChanged(bool value)
        {
            if (value)
            {
                OLService = DependencyService.Get<IOpenLibraryService>();
                EnsureSeriesAreLoaded();
            }
        }
        private async Task EnsureSeriesAreLoaded()
        {
            try
            {
                ShowSyncingToast();
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
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            string text = "Syncing with OpenLibrary";
            ToastDuration duration = ToastDuration.Short;
            double fontSize = 14;

            var toast = Toast.Make(text, duration, fontSize);
            await toast.Show(cancellationTokenSource.Token);
        }


    }
}
