using System.Globalization;

namespace MyNextBook.Converters
{
    public class ItemCountToHeightConverter : IValueConverter
    {
        private const double ROW_HEIGHT = 90; // Hardcoded value for each row height
        
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int count)
            {
                return count * ROW_HEIGHT;
            }
            
            return 0;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}