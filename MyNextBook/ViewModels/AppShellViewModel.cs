using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO;
using Microsoft.Maui.Storage;
using CommonCode.MSALClient; // MSAL helper
using Microsoft.Identity.Client;

namespace MyNextBook.ViewModels
{
    public partial class AppShellViewModel : ObservableObject
    {
        private const string AvatarFileName = "user_avatar.png"; // stored in AppDataDirectory
        private static string AvatarFilePath => Path.Combine(FileSystem.AppDataDirectory, AvatarFileName);

        [ObservableProperty] private ImageSource avatarImage;

        [ObservableProperty] private bool isMenuOpen;


        [ObservableProperty] private bool isSignedIn;

     

        public AppShellViewModel()
        {
         
            TryLoadExistingAvatar();
            _ = RefreshSignedInStateAsync();
        }

        private void OnGoToSettingsPage()
        {
            Shell.Current.GoToAsync("SettingsPage");
        }

        private void TryLoadExistingAvatar()
        {
            try
            {
                if (File.Exists(AvatarFilePath))
                {
                    AvatarImage = ImageSource.FromFile(AvatarFilePath);
                }
            }
            catch { /* ignore */ }
        }

        private async Task EnsurePcaInitializedAsync()
        {
            try
            {
                if (PublicClientSingleton.Instance.MSALClientHelper.PublicClientApplication == null)
                {
                    await PublicClientSingleton.Instance.MSALClientHelper.InitializePublicClientAppAsync();
                }
            }
            catch { /* ignore init errors here */ }
        }

        private async Task RefreshSignedInStateAsync()
        {
            try
            {
                await EnsurePcaInitializedAsync();
                var account = await PublicClientSingleton.Instance.MSALClientHelper.FetchSignedInUserFromCache();
                IsSignedIn = account != null;
            }
            catch
            {
                IsSignedIn = false;
            }
        }

        [RelayCommand] 
        private async Task ShowMenu()
        {
            System.Diagnostics.Debug.WriteLine("ShowMenu command called!");
            await RefreshSignedInStateAsync();
            IsMenuOpen = !IsMenuOpen;
            System.Diagnostics.Debug.WriteLine($"IsMenuOpen is now: {IsMenuOpen}");
        }
     

        [RelayCommand] private async Task SignInAsync()
        {
            try
            {
                await EnsurePcaInitializedAsync();
                await PublicClientSingleton.Instance.AcquireTokenSilentAsync();
                await RefreshSignedInStateAsync();
            }
            catch { }
            finally
            {
                IsMenuOpen = false;
            }
        }

        [RelayCommand] private async Task SignOutAsync()
        {
            try
            {
                await PublicClientSingleton.Instance.MSALClientHelper.SignOutUserAsync();
            }
            catch { }
            finally
            {
                await RefreshSignedInStateAsync();
                IsMenuOpen = false;
                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Shell.Current.GoToAsync("//MainPage/WelcomeScreen");
                });
            }
        }

        [RelayCommand] private async Task ChangeAvatar()
        {
            try
            {
                var result = await FilePicker.Default.PickAsync(new PickOptions
                {
                    PickerTitle = "Select an image",
                    FileTypes = FilePickerFileType.Images
                });

                if (result == null)
                    return;

                using var pickedStream = await result.OpenReadAsync();
                using var ms = new MemoryStream();
                await pickedStream.CopyToAsync(ms);
                var bytes = ms.ToArray();

                await SaveAvatarBytesAsync(bytes);
                AvatarImage = ImageSource.FromStream(() => new MemoryStream(bytes));
            }
            catch { }
            finally
            {
                IsMenuOpen = false; // close menu after edit
            }
        }

        private static async Task SaveAvatarBytesAsync(byte[] data)
        {
            try
            {
                var dir = FileSystem.AppDataDirectory;
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                await File.WriteAllBytesAsync(AvatarFilePath, data);
            }
            catch { }
        }
    }
}