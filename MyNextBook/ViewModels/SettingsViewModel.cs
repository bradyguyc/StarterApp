using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using System.Linq;
using DevExpress.Maui.Core;
using System.Reflection;
using CommonCode.Models;
using MyNextBook.Helpers;
using MyNextBook.Services;

namespace MyNextBook.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
     
        [ObservableProperty] private bool isCustomColorTheme = false;

        [ObservableProperty] private List<string> themeColorsList;

        [ObservableProperty] ShowPopUpDetails popupDetails;

        [ObservableProperty]
        private bool lightMode;

        [ObservableProperty]
        private string themeColor;

        [ObservableProperty]
        private string customColor; // Hex string, e.g. "#FF0000"

        [ObservableProperty]
        private string openLibraryUsername;

        [ObservableProperty]
        private string openLibraryPassword;

        private readonly IOpenLibraryService _OLService;
        public SettingsViewModel(IOpenLibraryService olService)
        {
            _OLService = olService;
            LoadSettings();
        }

        [RelayCommand]
        async Task TestOLCredentials()
        {
            try
            {
                PopupDetails = new ShowPopUpDetails();

                string OLUserName = await SecureStorage.Default.GetAsync(Constants.OpenLibraryUsernameKey);
                string OLPassword = await SecureStorage.Default.GetAsync(Constants.OpenLibraryPasswordKey);


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
                PopupDetails = new ShowPopUpDetails
                {
                    IsOpen = true,
                    ErrorMessage = ex.Message,
                    ErrorCode = "ERR-003"
                };
              
                OnPropertyChanged(nameof(PopupDetails));

                
            }
        }
        private async void LoadSettings()
        {
                LightMode = Preferences.Default.Get<bool>(Constants.LightModeKey, false);
                if (Preferences.Default.ContainsKey(Constants.ThemeColorKey)) ThemeColor = Preferences.Default.Get<string>(Constants.ThemeColorKey, "");
                if (Preferences.Default.ContainsKey(Constants.CustomColorKey)) CustomColor = Preferences.Default.Get<string>(Constants.CustomColorKey, "");

                ThemeColorsList = GetThemeSeedColorNames();
            


            OpenLibraryUsername = await SecureStorage.Default.GetAsync(Constants.OpenLibraryUsernameKey) ?? string.Empty;
            OpenLibraryPassword = await SecureStorage.Default.GetAsync(Constants.OpenLibraryPasswordKey) ?? string.Empty;
        }

        partial void OnLightModeChanged(bool value)
        {
            Application.Current.UserAppTheme = value ? AppTheme.Light : AppTheme.Dark;
            Preferences.Default.Set(Constants.LightModeKey, value);
        }

        partial void OnThemeColorChanged(string value)
        {
            Preferences.Default.Set(Constants.ThemeColorKey, value);

            if (value == "Custom")
            {
                //ShowThemeSeedColorPicker = true;

                Preferences.Default.Set("isCustomColorTheme", true);
                Preferences.Default.Set("themeColor", value);
                //show color picker
                //CustomColorTheme = ThemeSeedColor(Enum.Parse<ThemeSeedColor>(value));
            }
            else
            {
                Preferences.Default.Set("isCustomColorTheme", false);
                Preferences.Default.Set("themeColor", value);
                ThemeSeedColor themeSeedColor = Enum.Parse<ThemeSeedColor>(value);

                try
                {
                    ThemeManager.Theme = new Theme(themeSeedColor);
                }
                catch (Exception ex)
                {
                    throw new Exception("Unable to set standard theme color", ex);
                }

            }
        }

        partial void OnCustomColorChanged(string value)
        {
            Preferences.Default.Set(Constants.CustomColorKey, value);
        }

        partial void OnOpenLibraryUsernameChanged(string value)
        {
            SecureStorage.Default.SetAsync(Constants.OpenLibraryUsernameKey, value ?? string.Empty);
        }

        partial void OnOpenLibraryPasswordChanged(string value)
        {
            SecureStorage.Default.SetAsync(Constants.OpenLibraryPasswordKey, value ?? string.Empty);
        }

        // Add this method to your SettingsViewModel class
        public static List<string> GetThemeSeedColorNames()
        {
            // Assuming ThemeSeedColor is an enum or static class with color properties
            var type = typeof(ThemeSeedColor);
            // If ThemeSeedColor is an enum:
            // return Enum.GetNames(type).ToList();

            // If ThemeSeedColor is a static class with color properties:
            return type
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.FieldType == typeof(Color) || f.FieldType.Name.Contains("Color"))
                .Select(f => f.Name)
                .ToList();
        }

    }
}
