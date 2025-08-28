using MyNextBook.ViewModels;

namespace MyNextBook.Views;

public partial class BookSearchPage : ContentPage
{
    public BookSearchPage() { InitializeComponent(); }
    public BookSearchPage(BookSearchViewModel vm) : this()
    {
        BindingContext = vm;
    }
}
