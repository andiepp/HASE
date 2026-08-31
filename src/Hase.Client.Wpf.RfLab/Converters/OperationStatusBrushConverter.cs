#nullable enable

using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Hase.Client.Wpf.RfLab.Converters
{
    /// <summary>
    /// Paints the panel's status strip from the last operation outcome,
    /// replacing the protocol-library converter of the original application.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(Brush))]
    public sealed class OperationStatusBrushConverter : IValueConverter
    {
        public object Convert(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            return value is true
                ? Brushes.Firebrick
                : Brushes.Transparent;
        }

        public object ConvertBack(
            object value,
            Type targetType,
            object parameter,
            CultureInfo culture)
        {
            throw new NotSupportedException(
                "The operation-status brush is presentation only.");
        }
    }
}
