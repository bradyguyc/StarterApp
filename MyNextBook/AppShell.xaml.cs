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
        }
    }
}
