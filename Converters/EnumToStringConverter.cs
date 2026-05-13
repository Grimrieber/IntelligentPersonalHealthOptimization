using System.Globalization;
using System.Text.RegularExpressions;

namespace IntelligentPersonalHealthOptimization.Converters;

public partial class EnumToStringConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value == null) return string.Empty;

        var enumString = value.ToString() ?? string.Empty;
        return SplitCamelCase(enumString);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }

    private static string SplitCamelCase(string input)
    {
        return CamelCaseRegex().Replace(input, " $1");
    }

    [GeneratedRegex(@"(?<=[a-z])([A-Z])")]
    private static partial Regex CamelCaseRegex();
}
