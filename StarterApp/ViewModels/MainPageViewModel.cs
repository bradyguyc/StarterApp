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
        [ObservableProperty] private bool showErrorMessage = true; 
        [ObservableProperty] private string whatYouCanDo = "what you can do for brady guy";
        [ObservableProperty] private bool showWhat = true;
        [ObservableProperty] private string errorCode;
        [ObservableProperty] private string errorMessage = " this is an error message related to me and brady ";
        [ObservableProperty] private string errorReason = " this is an error reason for the brady error ";
        [ObservableProperty] private bool isOpen;
        [ObservableProperty] private string whatThisMeans= "what this means for brady guy chambers";
        [ObservableProperty] private string errorTitle = "this is a title for error Mr. Brady Guy Chambers";
        [ObservableProperty] private Color titleContainerColor = Color.Parse("White");
        [ObservableProperty] private Color titleOnContainerColor = Color.Parse("Black");


        public MainPageViewModel()
        {
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

            ErrorMessage = "Test Error message";
            ErrorReason = "Test Error Reason";

            IsOpen = true;
            ErrorCode = "ERR-001";
            OnPropertyChanged(nameof(ErrorCode));


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
                    if ((token == null) && (PopupDetails.IsOpen == false))
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