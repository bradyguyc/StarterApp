using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;

using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using CommonCode.Models;
using CommonCode.MSALClient;
using CommonCode.Helpers;
using System.Reflection;
using CommunityToolkit.Mvvm.Input;

namespace MyNextBook.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        [ObservableProperty] ShowPopUpDetails popupDetails;
        [ObservableProperty] private List<string> idTokenClaims = new();
        [ObservableProperty] private string introText = string.Empty;
        [ObservableProperty] private bool? signInEnabled = true;
        [ObservableProperty] private bool? showWelcome = false;
        private ILogger<MainPageViewModel> _logger;

        public bool IsSignedIn { get; private set; }

        public MainPageViewModel(ILogger<MainPageViewModel> logger)
        {
            _logger = logger;
            App.Current.UserAppTheme = AppTheme.Dark;
            PopupDetails = new ShowPopUpDetails();
            PopupDetails.IsOpen = false;
            InitializeAsync();
        }



        private async Task InitializeAsync()
        {
            //todo: I don't like loading error dictionary here.  Seems like it should be done in constructor. But had performance and timing issues.
            await ErrorDictionary.LoadErrorsFromFile();
            await MainThread.InvokeOnMainThreadAsync(async () =>
            {

                IntroText = await CommonCode.Helpers.FileHelpers.ReadTextFile("introtext.txt");

                var cachedUserAccount =
                    await PublicClientSingleton.Instance.MSALClientHelper.FetchSignedInUserFromCache();
                IsSignedIn = cachedUserAccount != null;
            });
            if (IsSignedIn == true)
                await Shell.Current.GoToAsync("MySeriesPage/SettingsPage");
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
                if (IsSignedIn == true)
                {
                    await Shell.Current.GoToAsync("SettingsPage");
                }
            });
          
           
        }

    }
}
