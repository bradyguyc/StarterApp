using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;

using StarterApp.Models;
using StarterApp.MSALClient;
using StarterApp.Services;

namespace StarterApp.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        [ObservableProperty] ShowPopUpDetails popupDetails;
        [ObservableProperty] private bool isSignedIn = false;
        [ObservableProperty] private List<string> idTokenClaims = new();

        private readonly ILogger<MainPageViewModel> _logger;


        public MainPageViewModel(ILogger<MainPageViewModel> logger)
        {
            _logger = logger;
            PopupDetails = new ShowPopUpDetails();

            IsSignedIn = false;
            PopupDetails.IsOpen = false;
            IAccount cachedUserAccount = PublicClientSingleton.Instance.MSALClientHelper.FetchSignedInUserFromCache().Result;
            if (cachedUserAccount != null)
            {
                IsSignedIn = true;
                UpdateClaims();
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
            IAccount? cachedUserAccount = null; ;
            cachedUserAccount = PublicClientSingleton.Instance.MSALClientHelper.FetchSignedInUserFromCache().Result;

            return MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (cachedUserAccount == null)
                {
                    string token = null;
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
                        //Console.WriteLine(ex.Message);
                    }
                    if ((token == null) && (PopupDetails.IsOpen == false))
                    {
                        PopupDetails.IsOpen = true;
                        PopupDetails.ErrorCode = "ERR-002 ";
                        OnPropertyChanged(nameof(PopupDetails));
                        //todo: display error message to user that they need to sign in and the sign process was exited before completing sign in
                    }
                    else
                    {
                        IAccount cachedUserAccount = PublicClientSingleton.Instance.MSALClientHelper.FetchSignedInUserFromCache().Result;
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
        public Task CallAzureFunction()
        {
            return MainThread.InvokeOnMainThreadAsync(async () =>
            {
                if (!IsSignedIn)
                {
                    PopupDetails.IsOpen = true;
                    PopupDetails.ErrorCode = "ERR-003";
                    PopupDetails.ErrorMessage = "You must be signed in to call the Azure Function.";
                    return;
                }
                try
                {
                    // Call your Azure Function here
                    await Task.Run(async () =>
                    {
                        bool success = await GetSecrets.Instance.InitGetSecrets();
                        if (success)
                        {
                            PopupDetails.IsOpen = true;
                            PopupDetails.ErrorCode = "INFO-001";
                            PopupDetails.ErrorMessage = "Azure Function called successfully and secrets initialized.";
                            OnPropertyChanged(nameof(PopupDetails));
                          
                        } else
                        {
                            PopupDetails.IsOpen = true;
                            PopupDetails.ErrorCode = "ERR-003";
                            PopupDetails.ErrorMessage = "Failed to initialize secrets from Azure Function.";
                            OnPropertyChanged(nameof(PopupDetails));

                        }
                    }
                    ); // Ensure secrets are initialized before calling the function

                    // Handle the result as needed
                }
                catch (Exception ex)
                {
                    PopupDetails.IsOpen = true;
                    PopupDetails.ErrorCode = "ERR-004";
                    PopupDetails.ErrorMessage = ex.Message;
                }
            });
        }
    }
}