using System.Text.RegularExpressions;

namespace IntelligentPersonalHealthOptimization.Helpers;

/// <summary>
/// Humanizes PascalCase enum names for display ("GlutenFree" -> "Gluten Free",
/// "TreeNuts" -> "Tree Nuts"). Use in code paths that show enum names directly
/// (action sheets, summaries); XAML pickers should use the EnumToString converter
/// or PickerItem. Reverse with value.Replace(" ", "") before Enum.TryParse.
/// </summary>
public static partial class EnumDisplay
{
    public static string Humanize(string? s) =>
        string.IsNullOrEmpty(s) ? (s ?? string.Empty) : CamelCaseRegex().Replace(s, " $1");

    public static string Humanize(Enum value) => Humanize(value.ToString());

    [GeneratedRegex(@"(?<=[a-z])([A-Z])")]
    private static partial Regex CamelCaseRegex();
}
