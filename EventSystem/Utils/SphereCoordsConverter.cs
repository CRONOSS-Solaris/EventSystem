using System;
using System.Globalization;
using System.Windows.Data;
using static EventSystem.Event.WarZone;
using static EventSystem.Event.WarZoneGrid;

namespace EventSystem.Utils
{
    public class SphereCoordsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Sprawdzenie, czy wartość jest jednym z typów SphereCoords
            if (value is SphereCoords coords)
            {
                return $"{coords.X}, {coords.Y}, {coords.Z}";
            }
            else if (value is SphereCoordsGrid coordsGrid)
            {
                // Obsługa SphereCoordsGrid, jeśli jest to ten typ
                return $"{coordsGrid.X}, {coordsGrid.Y}, {coordsGrid.Z}";
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string strValue)
            {
                var parts = strValue.Split(',');
                if (parts.Length == 3 && double.TryParse(parts[0], out double x) && double.TryParse(parts[1], out double y) && double.TryParse(parts[2], out double z))
                {
                    // Możesz użyć targetType, aby określić, który typ zwrócić
                    if (targetType == typeof(SphereCoords))
                    {
                        return new SphereCoords(x, y, z);
                    }
                    else if (targetType == typeof(SphereCoordsGrid))
                    {
                        return new SphereCoordsGrid(x, y, z);
                    }
                }
            }
            return null;
        }
    }
}
