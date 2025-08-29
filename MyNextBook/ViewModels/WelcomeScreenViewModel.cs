using System;
using System.Collections.Generic;
using System.Text;

using CommonCode.Models;
using CommonCode.MSALClient;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using ImportSeries;

using Microsoft.Extensions.Logging;

using MyNextBook.Helpers;
using MyNextBook.Services;

namespace MyNextBook.ViewModels
{

    public partial class WelcomeScreenViewModel : ObservableObject
    {
        [ObservableProperty] private string introText;
        [ObservableProperty] ShowPopUpDetails popupDetails;
        [ObservableProperty] private bool loggedIn = false;
        [ObservableProperty] private bool olCredentialsSet = false;
        [ObservableProperty] private string userName = "Not logged in";
        [ObservableProperty] private string olUserName = "Not logged in";
        private readonly IOpenLibraryService _OLService;
        private readonly ILogger<WelcomeScreenViewModel> _logger;

        public WelcomeScreenViewModel(ILogger<WelcomeScreenViewModel> logger, IOpenLibraryService olService)
        {
            _logger = logger;
            InitializeAsync();
        }

      
        private async Task InitializeAsync()
        {
            PopupDetails = new ShowPopUpDetails
            {
                IsOpen = false
            };

            IntroText = await CommonCode.Helpers.FileHelpers.ReadTextFile("introtext.html");
            if (string.IsNullOrWhiteSpace(IntroText))
            {
                IntroText = "Welcome to My Next Book! This app helps you track your reading journey.";
            }

            await UpdateLoginStateAsync();
            OlCredentialsSet = await AreOLCredentialsSetAsync();
        }

        private async Task UpdateLoginStateAsync()
        {
            try
            {
                // Assuming MSAL wrapper exposes fetching cached account/user
                var account = await PublicClientSingleton.Instance.FetchSignedInUserFromCacheAsync();

                if (account is null)
                {
                    LoggedIn = false;
                    UserName = "Not logged in";
                    return;
                }

                LoggedIn = true;

                // Try common property names defensively via reflection to avoid tight coupling.
                string? displayName = null;
                try
                {
                    displayName =
                        account.GetType().GetProperty("Username")?.GetValue(account)?.ToString()
                        ?? account.GetType().GetProperty("UserName")?.GetValue(account)?.ToString()
                        ?? account.GetType().GetProperty("DisplayName")?.GetValue(account)?.ToString()
                        ?? account.GetType().GetProperty("HomeAccountId")?.GetValue(account)?.ToString();
                }
                catch
                {
                    // Ignore reflection issues; fallback below.
                }

                UserName = string.IsNullOrWhiteSpace(displayName) ? "Signed in user" : displayName;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to update login state from cache.");
                LoggedIn = false;
                UserName = "Not logged in";
            }
        }

        private async Task<bool> AreOLCredentialsSetAsync()
        {
            try
            {
                // Key names assumed; adjust if actual keys differ.
                var olUser = await SecureStorage.GetAsync(Constants.OpenLibraryUsernameKey);
                var olKey = await SecureStorage.GetAsync(Constants.OpenLibraryPasswordKey);
                if (!string.IsNullOrWhiteSpace(olUser) && !string.IsNullOrWhiteSpace(olKey))
                {
                    OlUserName = olUser;
                }
                return !string.IsNullOrWhiteSpace(olUserName);
                    }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unable to read Open Library credentials from secure storage.");
                return false;
            }
        }

        [RelayCommand]
        public async Task SignIn()
        {
            string token = null;
            try
            {
                token = await PublicClientSingleton.Instance.AcquireTokenSilentAsync();
            }
            catch (Exception ex)
            {
                PopupDetails.IsOpen = true;
                PopupDetails.ErrorMessage = ex.Message;
                PopupDetails.ErrorCode = "ERR-001";
                return;
            }

            try
            {
                if (string.IsNullOrEmpty(token))
                {
                    PopupDetails.IsOpen = true;
                    PopupDetails.ErrorCode = "ERR-002";
                    return;
                }

                await UpdateLoginStateAsync();

                OlCredentialsSet = await AreOLCredentialsSetAsync();
                bool credentialAvailable = OlCredentialsSet;

                if (!credentialAvailable)
                {
                    await Shell.Current.GoToAsync("SettingsPage");
                }
                else
                {
                    await Shell.Current.GoToAsync("..");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during sign-in process");
                PopupDetails.IsOpen = true;
                PopupDetails.ErrorMessage = ex.Message;
                PopupDetails.ErrorCode = "ERR-003";
            }
        }
        [RelayCommand]
        async Task TestOLCredentials()
        {
            try
            {
                PopupDetails = new ShowPopUpDetails();

                string OLUserName = await SecureStorage.Default.GetAsync(Constants.OpenLibraryUsernameKey);
                string OLPassword = await SecureStorage.Default.GetAsync(Constants.OpenLibraryPasswordKey);

                _OLService.SetUsernamePassword(OLUserName, OLPassword);
                bool r = await _OLService.Login();
                if (r == true)
                {

                    PopupDetails.IsOpen = true;

                    PopupDetails.ErrorCode = "INFO-001";
                    OnPropertyChanged(nameof(PopupDetails));

                }
                else
                {
                    PopupDetails.IsOpen = true;

                    PopupDetails.ErrorCode = "ERR-003";
                    OnPropertyChanged(nameof(PopupDetails));

                }

            }
            catch (Exception ex)
            {
                PopupDetails.IsOpen = true;

                PopupDetails.ErrorCode = "OL-003";
                PopupDetails.ErrorMessage = $"Could not sign in to OpenLibrary. Please check your credentials and/or network.\n{ex.Message}";
                OnPropertyChanged(nameof(PopupDetails));
            }
        }

    }
}