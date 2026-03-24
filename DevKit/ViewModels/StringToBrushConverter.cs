using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace DevKit.ViewModels
{
    public class StringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is string hex && hex.StartsWith("#") && hex.Length == 7)
                try { return new SolidColorBrush(Color.FromRgb(System.Convert.ToByte(hex.Substring(1, 2), 16), System.Convert.ToByte(hex.Substring(3, 2), 16), System.Convert.ToByte(hex.Substring(5, 2), 16))); } catch { }
            return new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86));
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => throw new NotImplementedException();
    }
}
