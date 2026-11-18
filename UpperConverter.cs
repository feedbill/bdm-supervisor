using System.Globalization;
using System.Windows.Data;

namespace BdmSupervisor;

/// <summary>Pasa a mayúsculas el texto (ya traducido) de las entradas del menú lateral.</summary>
public sealed class UpperConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => (value as string ?? value?.ToString() ?? "").ToUpper(culture);
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
