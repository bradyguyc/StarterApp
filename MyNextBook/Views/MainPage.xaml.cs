using MyNextBook.ViewModels;

namespace MyNextBook
{
    public partial class MainPage : ContentPage
    {
        public MainPage(MainPageViewModel vm)
        {
            InitializeComponent();
            BindingContext = vm;
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            if (BindingContext is MainPageViewModel vm && vm.AppearingCommand.CanExecute(null))
            {
                vm.AppearingCommand.Execute(null);
            }
        }
    }
}
