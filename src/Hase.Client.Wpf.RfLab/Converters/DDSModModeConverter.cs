using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Hase.Client.Wpf.RfLab.ViewModels;

namespace Hase.Client.Wpf.RfLab.Converters
{
    [ValueConversion(typeof(RfLabSignalMode), typeof(bool))]
    public class DDSModModeConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return ((string)parameter == ((int)value).ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            return (bool)value ? parameter : null;
        }
    }
}
