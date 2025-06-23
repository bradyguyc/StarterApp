using System;
using System.Collections.Generic;
using System.Text;

using CommonCode.Models;
using CommonCode.MSALClient;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Microsoft.Extensions.Logging;

using MyNextBook.Helpers;
using MyNextBook.Services;

namespace MyNextBook.ViewModels
{

    public partial class WelcomeScreenViewModel : ObservableObject
    {
        [ObservableProperty] private string introText;
        [ObservableProperty] ShowPopUpDetails popupDetails;
        private readonly ILogger<WelcomeScreenViewModel> _logger;
        public WelcomeScreenViewModel(ILogger<WelcomeScreenViewModel> logger)
        {
            _logger = logger;
            InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            PopupDetails = new ShowPopUpDetails();       
            PopupDetails.IsOpen = false;
            IntroText = await CommonCode.Helpers.FileHelpers.ReadTextFile("introtext.txt");
            if (string.IsNullOrWhiteSpace(IntroText))
            {
                IntroText = "Welcome to My Next Book! This app helps you track your reading journey.";
            }
        }
        [RelayCommand]
        public async Task SignIn()
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

                return;
            }
            try
            {
                if (string.IsNullOrEmpty(token))
                {


                    PopupDetails.IsOpen = true;
                    PopupDetails.ErrorCode = "ERR-002 ";

                }


                bool credentialAvailable = await StaticHelpers.OLAreCredentialsSetAsync();
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
    }
}