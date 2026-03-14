using System;
using System.Globalization;
using System.Windows.Data;

namespace XdcLocalDesktopAccessTool.App.Converters
{
    public sealed class IndexToDisplayNumberConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is int i) return (i + 1).ToString(culture);
            if (value is string s && int.TryParse(s, out var parsed)) return (parsed + 1).ToString(culture);
            return "";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
