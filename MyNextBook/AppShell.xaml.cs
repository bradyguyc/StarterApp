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

            // With explicit ShellContent routes, use absolute routes based on tab content
            Routing.RegisterRoute("MainPage/SettingsPage", typeof(SettingsPage));
            Routing.RegisterRoute("MainPage/WelcomeScreen", typeof(WelcomeScreen));
            Routing.RegisterRoute("SettingsPage/ImportCSV", typeof(ImportCSV));
        }
        
        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);

            pageTitle.Text = Current.CurrentPage.Title;
        }
        
    }
}
