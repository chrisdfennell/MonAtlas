using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MonAtlas.Converters
{
    // Converts empty TextBox text → Visible placeholder
    // Non-empty → Collapsed placeholder
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public static readonly BooleanToVisibilityConverter Instance = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Show placeholder if text is null or empty
            bool isEmpty = string.IsNullOrEmpty(value as string);
            return isEmpty ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
