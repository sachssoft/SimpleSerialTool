using Avalonia.Data;
using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace sachssoft.SimpleSerialTool.Converters
{
    internal class EnumToBoolConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.Equals(parameter);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            return value?.Equals(true) == true ? parameter : BindingOperations.DoNothing;
        }
    }
}
