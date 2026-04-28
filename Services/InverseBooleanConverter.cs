using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace AlgorithmVisualizer.Converters
{
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool b))
                return value;

            if (targetType == typeof(Visibility))
            {
                // Konwerter ukrywa element, jeśli wartość logiczna to 'true'.
                return b ? Visibility.Collapsed : Visibility.Visible;
            }

            return !b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotImplementedException();
    }
}