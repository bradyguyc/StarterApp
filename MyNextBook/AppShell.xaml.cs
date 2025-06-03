using Microsoft.Maui.Controls;
using MyNextBook.Views;
using MyNextBook.ViewModels;

namespace MyNextBook
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            BindingContext = new AppShellViewModel();
            Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
            Routing.RegisterRoute("MainPage/SettingsPage", typeof(SettingsPage));
        }
        
        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);

            pageTitle.Text = Current.CurrentPage.Title;
        }
        
    }
}
