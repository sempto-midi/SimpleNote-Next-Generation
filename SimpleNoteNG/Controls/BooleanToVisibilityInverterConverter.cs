﻿using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SimpleNoteNG.Controls
{
    public class BooleanToVisibilityInverterConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool booleanValue)
                return booleanValue ? Visibility.Collapsed : Visibility.Visible;
            return Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}