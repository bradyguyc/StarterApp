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
        private readonly IOpenLibraryService OLService;
        private readonly ILogger<MainPageViewModel> _logger;

        [ObservableProperty] private bool isSignedIn;

        public MainPageViewModel(IOpenLibraryService olService, ILogger<MainPageViewModel> logger)
        {
            OLService = olService;
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

                IntroText = await CommonCode.Helpers.FileHelpers.ReadTextFile("introtext.txt");

                var cachedUserAccount =
                    await PublicClientSingleton.Instance.MSALClientHelper.FetchSignedInUserFromCache();
                IsSignedIn = cachedUserAccount != null;
            });
            if (IsSignedIn == false)
            {
                //await UpdateClaims();
                ShowWelcome = true;
                ShowSeries = false;
                //Application.Current.Windows[0].Page = new MySeriesPage();


            }
            else
            {
                bool credentialAvailable = await StaticHelpers.OLAreCredentialsSetAsync();

                if (!credentialAvailable)
                {
                    //Application.Current.Windows[0].Page = new MySeriesPage();
                    await Shell.Current.GoToAsync("SettingsPage");

                }
                else
                {
                    ShowWelcome = false;
                    ShowSeries = true;
                }
            }


        }

        [RelayCommand]
        async Task Appearing()
        {
            if (!IsSignedIn)
            {
                ShowSeries = false;
                ShowWelcome = true;
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
                OnPropertyChanged(nameof(PopupDetails));
                return;
            }
        }
        [RelayCommand]
        async void GoToSettingsPage()
        {
            await Shell.Current.GoToAsync("SettingsPage");

        }
        [RelayCommand]
        public Task SignIn()
        {

            IAccount? cachedUserAccount = null;
            SignInEnabled = false;
            cachedUserAccount = PublicClientSingleton.Instance.MSALClientHelper.FetchSignedInUserFromCache().Result;

            return MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (cachedUserAccount == null)
                {
                    string? token = null;
                    try
                    {
                        token = await PublicClientSingleton.Instance.AcquireTokenSilentAsync();
                    }
                    catch (Exception ex)
                    {
                        PopupDetails.IsOpen = true;
                        SignInEnabled = true;

                        PopupDetails.ErrorMessage = ex.Message;
                        PopupDetails.ErrorCode = "ERR-001";
                        OnPropertyChanged(nameof(PopupDetails));
                        return;
                    }
                    if ((token == null) && (PopupDetails.IsOpen == false))
                    {
                        SignInEnabled = true;
                        PopupDetails.IsOpen = true;
                        PopupDetails.ErrorCode = "ERR-002 ";
                        OnPropertyChanged(nameof(PopupDetails));
                    }
                    else
                    {
                        cachedUserAccount = PublicClientSingleton.Instance.MSALClientHelper.FetchSignedInUserFromCache().Result;
                        if (cachedUserAccount != null)
                        {
                            IsSignedIn = true;

                            //await UpdateClaims();
                        }
                        else SignInEnabled = true;
                    }
                }
                bool credentialAvailable = await StaticHelpers.OLAreCredentialsSetAsync();
                if ((IsSignedIn == true) && (credentialAvailable))
                {
                    //await UpdateClaims();
                    ShowWelcome = false;
                    //Application.Current.Windows[0].Page = new MySeriesPage();
                }
                else if (IsSignedIn == true && !credentialAvailable)
                {
                    //Application.Current.Windows[0].Page = new MySeriesPage();
                    await Shell.Current.GoToAsync("SettingsPage");

                }

            });


        }
        partial void OnIsSignedInChanged(bool value)
        {
            if (value)
            {
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
                OnPropertyChanged(nameof(PopupDetails));
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
