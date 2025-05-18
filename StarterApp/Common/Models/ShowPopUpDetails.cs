using System;
using System.Collections.Generic;
using System.Text;


using CommunityToolkit.Mvvm;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;


namespace StarterApp.Models
{
    public partial class ShowPopUpDetails : ObservableObject
    {
        [ObservableProperty] private bool isOpen;
       // [ObservableProperty] private string errorTitle;
        [ObservableProperty] private string? errorMessage;
        [ObservableProperty] private string? errorCode;
        [ObservableProperty] private string? errorReason;

    }
}
