using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Storage;
using System.Linq;

namespace MyNextBook.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private const string LightModeKey = "LightMode";
        private const string ThemeColorKey = "ThemeColor";
        private const string CustomColorKey = "CustomColor";
        private const string OpenLibraryUsernameKey = "OpenLibraryUsername";
        private const string OpenLibraryPasswordKey = "OpenLibraryPassword";

        public List<string> ThemeColors { get; } = new()
        {
            "Blue", "Red", "Green", "Orange", "Purple", "Custom"
        };

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

        public SettingsViewModel()
        {
            LoadSettings();
        }

        private async void LoadSettings()
        {
            LightMode = Preferences.Default.Get(LightModeKey, false);
            ThemeColor = Preferences.Default.Get(ThemeColorKey, ThemeColors.First());
            CustomColor = Preferences.Default.Get(CustomColorKey, "#2196F3");

            OpenLibraryUsername = await SecureStorage.Default.GetAsync(OpenLibraryUsernameKey) ?? string.Empty;
            OpenLibraryPassword = await SecureStorage.Default.GetAsync(OpenLibraryPasswordKey) ?? string.Empty;
        }

        partial void OnLightModeChanged(bool value)
        {
            Preferences.Default.Set(LightModeKey, value);
        }

        partial void OnThemeColorChanged(string value)
        {
            Preferences.Default.Set(ThemeColorKey, value);
        }

        partial void OnCustomColorChanged(string value)
        {
            Preferences.Default.Set(CustomColorKey, value);
        }

        partial void OnOpenLibraryUsernameChanged(string value)
        {
            SecureStorage.Default.SetAsync(OpenLibraryUsernameKey, value ?? string.Empty);
        }

        partial void OnOpenLibraryPasswordChanged(string value)
        {
            SecureStorage.Default.SetAsync(OpenLibraryPasswordKey, value ?? string.Empty);
        }
    }
}
