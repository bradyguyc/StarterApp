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

     
    }
}
