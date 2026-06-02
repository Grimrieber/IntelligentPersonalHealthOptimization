using System.Globalization;

namespace IntelligentPersonalHealthOptimization.Converters;

/// <summary>
/// true → fully opaque (1.0), false → dimmed (0.25). Drives the save/favorite
/// star's brightness via a direct binding, which re-evaluates reliably on
/// property change — a DataTrigger on Opacity didn't apply on the recipe
/// detail page (the trigger setter never overrode the element's value there).
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public double TrueOpacity { get; set; } = 1.0;
    public double FalseOpacity { get; set; } = 0.25;

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is bool b && b ? TrueOpacity : FalseOpacity;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
