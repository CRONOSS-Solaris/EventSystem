using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using VRage;

namespace EventSystem.Utils
{
    public class ColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SerializableVector3 vec)
            {
                return Color.FromArgb(255, (byte)(vec.X * 255), (byte)(vec.Y * 255), (byte)(vec.Z * 255));
            }
            return Colors.Black; // Domyślny kolor
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Color color)
            {
                return new SerializableVector3(color.R / 255.0f, color.G / 255.0f, color.B / 255.0f);
            }
            return new SerializableVector3(0f, 0f, 0f); // Domyślny wektor
        }
    }
}
