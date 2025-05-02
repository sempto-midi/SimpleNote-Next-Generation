using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SimpleNoteNG.Converters
{
    public class EmailStatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isConfirmed)
            {
                return isConfirmed ? Brushes.LightGreen : Brushes.OrangeRed;
            }
            return Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}