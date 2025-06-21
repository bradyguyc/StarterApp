using MyNextBook.ViewModels;

namespace MyNextBook.Views;

public partial class WelcomeScreen : ContentPage
{
	public WelcomeScreen(WelcomeScreenViewModel vm)
	{
		InitializeComponent();
		BindingContext = vm;
    }
}