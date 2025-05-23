using MyNextBook.Views;
namespace MyNextBook
{
    public partial class AppShell : Shell
    {
        public AppShell()
        {
            InitializeComponent();
            Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
            Routing.RegisterRoute("MySeriesPage", typeof(MySeriesPage));
            Routing.RegisterRoute("MySeriesPage/SettingsPage", typeof(SettingsPage));

        }
        /*
        protected override void OnNavigated(ShellNavigatedEventArgs args)
        {
            base.OnNavigated(args);


            //pageTitle.Text = Current.CurrentPage.Title;
        }
        */
    }
}
