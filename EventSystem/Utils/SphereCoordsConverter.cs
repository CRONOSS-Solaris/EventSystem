using static EventSystem.Event.WarZone;
using System.Globalization;
using System.Windows.Data;
using System;

namespace EventSystem.Utils
{
    public class SphereCoordsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is SphereCoords coords)
            {
                return $"{coords.X}, {coords.Y}, {coords.Z}";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                var parts = strValue.Split(',');
                if (parts.Length == 3)
                {
                    if (double.TryParse(parts[0], out double x) &&
                        double.TryParse(parts[1], out double y) &&
                        double.TryParse(parts[2], out double z))
                    {
                        return new SphereCoords(x, y, z);
                    }
                }
            }
            return null;
        }
    }
}
