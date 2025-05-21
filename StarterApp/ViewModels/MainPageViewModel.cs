using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

using CommonCode.Models;
using CommonCode.MSALClient;
using StarterApp.Services;

namespace StarterApp.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        [ObservableProperty] ShowPopUpDetails popupDetails;
        [ObservableProperty] private bool isSignedIn = false;
        [ObservableProperty] private List<string> idTokenClaims = new();

        private readonly ILogger<MainPageViewModel> _logger;
      

        public MainPageViewModel(
            ILogger<MainPageViewModel> logger)
         
        {
            _logger = logger;
         
            PopupDetails = new ShowPopUpDetails();
            IsSignedIn = false;
            PopupDetails.IsOpen = false;

            IAccount? cachedUserAccount = PublicClientSingleton.Instance.MSALClientHelper.FetchSignedInUserFromCache().Result;
            if (cachedUserAccount != null)
            {
                IsSignedIn = true;
                _ = UpdateClaims();
            }
        }

        [RelayCommand]
        void TestShowError()
        {

            PopupDetails.ErrorMessage = "Exception: User Cancelled process: stacktrace: ........";
            PopupDetails.ErrorReason = "From app launch get user login information.";
            PopupDetails.IsOpen = true;
            PopupDetails.ErrorCode = "ERR-001";
            OnPropertyChanged(nameof(PopupDetails));
        }
        [RelayCommand]
        void TestShowInfo()
        {

            PopupDetails.ErrorMessage = "Exception: User Cancelled process: stacktrace: ........";
            PopupDetails.ErrorReason = "From app launch get user login information.";
            PopupDetails.IsOpen = true;
            PopupDetails.ErrorCode = "ERR-002";
            OnPropertyChanged(nameof(PopupDetails));
        }
        private string GetClaimValue(Claim claim)
        {
            switch (claim.Type)
            {
                case ClaimTypes.Name:
                case ClaimTypes.Email:
                case ClaimTypes.Role:
                    return claim.Value;

                case ClaimTypes.DateOfBirth:
                    if (DateTime.TryParse(claim.Value, out var dateOfBirth))
                    {
                        return dateOfBirth.ToShortDateString();
                    }
                    break;

                case ClaimTypes.Sid:
                case ClaimTypes.NameIdentifier:
                    return claim.Value;

                // Add more cases as needed for other claim types

                default:
                    return claim.Value;
            }

            return claim.Value;
        }

        private async Task UpdateClaims()
        {

            _ = await PublicClientSingleton.Instance.AcquireTokenSilentAsync();

            var claims = PublicClientSingleton.Instance
                    .MSALClientHelper.AuthResult.ClaimsPrincipal.Claims.Select(c => $"{c.Type}: {GetClaimValue(c)}");

            IdTokenClaims = claims.ToList();
        }
        [RelayCommand] void CloseErrorPopup ()
        {
            PopupDetails.IsOpen = false;
        }
        [RelayCommand]
        public Task SignIn()
        {
            IAccount? cachedUserAccount = null;
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
                        PopupDetails.ErrorCode = "ERR-001";
                        PopupDetails.ErrorMessage = ex.Message;
                        OnPropertyChanged(nameof(PopupDetails));
                    }
                    if ((token == null) && (PopupDetails.IsOpen == false))
                    {
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
                            await UpdateClaims();
                        }
                    }
                }
            });
        }

        [RelayCommand]
        public Task SignOut()
        {
            return MainThread.InvokeOnMainThreadAsync(async () =>
            {
                await PublicClientSingleton.Instance.MSALClientHelper.SignOutUserAsync();
                IsSignedIn = false;
                IdTokenClaims = new List<string>();
            });
        }
        [RelayCommand]
        public async Task CallAzureFunction(CancellationToken cancellationToken = default)
        {
            if (!IsSignedIn)
            {
                ShowError("ERR-003", "You must be signed in to call the Azure Function.");
                return;
            }

            try
            {
                //todo: not sure this is the best way to do this
                // Get service from service provider
                var serviceProvider = Application.Current?.Handler?.MauiContext?.Services;
                if (serviceProvider == null)
                {
                    ShowError("ERR-005", "Unable to access application services");
                    return;
                }

                var secretService = serviceProvider.GetService<IGetSecrets>();
                if (secretService == null)
                {
                    ShowError("ERR-006", "Secret service not configured");
                    return;
                }
                _logger.LogInformation("Calling GetSecretAsync...");
                string secret = await secretService.GetSecretAsync("brady")
                    .ConfigureAwait(false);
                _logger.LogInformation("GetSecretAsync completed");

                if (!string.IsNullOrWhiteSpace(secret))
                {
                    ShowInfo("INFO-001", "Azure Function called successfully and secrets initialized.");
                }
                else
                {
                    ShowError("ERR-003", "Failed to initialize secrets from Azure Function.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error calling Azure Function");
                var errorMessage = $"{ex.Message}";
                if (ex.InnerException != null)
                {
                    errorMessage += $"\nInner Exception: {ex.InnerException.Message}";
                }
                ShowError("ERR-004", errorMessage);
            }
        }

        private void ShowError(string errorCode, string message)
        {
            PopupDetails.IsOpen = true;
            PopupDetails.ErrorCode = errorCode;
            PopupDetails.ErrorMessage = message;
            OnPropertyChanged(nameof(PopupDetails));
        }

        private void ShowInfo(string code, string message)
        {
            PopupDetails.IsOpen = true;
            PopupDetails.ErrorCode = code;
            PopupDetails.ErrorMessage = message;
            OnPropertyChanged(nameof(PopupDetails));
        }

   
    }
}