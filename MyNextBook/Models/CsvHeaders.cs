using CommunityToolkit.Mvvm.ComponentModel;

namespace MyNextBook.Models
{
    public partial  class CsvHeaders : ObservableObject
    {
        [ObservableProperty]
        private string objectName;

        [ObservableProperty]
        private string propertyName;
        [ObservableProperty]
        private bool propertyExists;
        [ObservableProperty] private int order;
    }
}
