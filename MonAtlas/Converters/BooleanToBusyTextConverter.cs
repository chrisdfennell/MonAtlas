using System;
using System.Globalization;
using System.Windows.Data;

namespace MonAtlas.Converters
{
    public class BooleanToBusyTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isBusy = value is bool b && b;
            return isBusy ? "Searching..." : "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
