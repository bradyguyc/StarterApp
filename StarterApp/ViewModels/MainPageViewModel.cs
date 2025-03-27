using System;
using System.Collections.Generic;
using System.Text;

using StarterApp.Models;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StarterApp.ViewModels
{
    public partial class MainPageViewModel : ObservableObject
    {
        [ObservableProperty] ShowPopUpDetails popupDetails;
        public MainPageViewModel()
        {
            PopupDetails = new ShowPopUpDetails();
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
    }
}