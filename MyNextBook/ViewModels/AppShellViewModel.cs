using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;

namespace MyNextBook.ViewModels
{
    public class AppShellViewModel
    {
        public ICommand GoToSettingsPageCommand { get; }

        public AppShellViewModel()
        {
            GoToSettingsPageCommand = new RelayCommand(OnGoToSettingsPage);
        }

        private void OnGoToSettingsPage()
        {
            // Navigation logic here, e.g.:
            Shell.Current.GoToAsync("SettingsPage");
        }
    }
}