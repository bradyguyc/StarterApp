using System;
using System.Collections.Generic;
using System.Text;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Identity.Client;

using StarterApp.Models;
using StarterApp.MSALClient;

namespace StarterApp.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        [ObservableProperty] ShowPopUpDetails popupDetails;
        [ObservableProperty] private bool isSignedIn = false;
        [ObservableProperty] private List<string> idTokenClaims = new();

        [ObservableProperty] private bool showErrorPopUp = false;
        [ObservableProperty] private bool errorSuccess = false;
        [ObservableProperty] private ShowPopUpDetails errorDetails = new ShowPopUpDetails();
        public MainPageViewModel()
        {
            PopupDetails = new ShowPopUpDetails();

            IsSignedIn = false;
            ErrorDetails.IsOpen = false;
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
            PopupDetails.ErrorCode = "ERR-001";
            PopupDetails.ErrorMessage = "Test Error message";
            PopupDetails.ErrorReason = "Test Error Reason";
            PopupDetails.IsOpen = true;
        }

        [RelayCommand]
        void ClosePopUp()
        {
            PopupDetails.IsOpen = false;
        }

        private async Task UpdateClaims()
        {
            _ = await PublicClientSingleton.Instance.AcquireTokenSilentAsync();

            var claims = PublicClientSingleton.Instance.MSALClientHelper.AuthResult.ClaimsPrincipal.Claims.Select(c => c.Value);

            IdTokenClaims = claims.ToList();
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
                        //Console.WriteLine(ex.Message);
                    }
                    if ((token == null) && (ErrorDetails.IsOpen == false))
                    {
                        PopupDetails.IsOpen = true;
                        PopupDetails.ErrorCode = "ERR-001";
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
            });
        }
    }
}