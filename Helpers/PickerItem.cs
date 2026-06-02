using System.Text.RegularExpressions;

namespace IntelligentPersonalHealthOptimization.Helpers;

/// <summary>
/// Wraps an enum value with a humanized display string for use in MAUI Pickers.
/// Picker shows ToString(), so the display label is what's rendered.
/// </summary>
public partial class PickerItem<T> where T : notnull
{
    public string Display { get; }
    public T Value { get; }

    public PickerItem(T value) : this(value, Humanize(value.ToString() ?? string.Empty)) { }
    public PickerItem(T value, string display)
    {
        Value = value;
        Display = display;
    }

    public override string ToString() => Display;

    public override bool Equals(object? obj) =>
        obj is PickerItem<T> other && EqualityComparer<T>.Default.Equals(Value, other.Value);

    public override int GetHashCode() => Value.GetHashCode();

    public static List<PickerItem<T>> From(IEnumerable<T> values) =>
        values.Select(v => new PickerItem<T>(v)).ToList();

    private static string Humanize(string input) =>
        string.IsNullOrEmpty(input) ? input : CamelCaseRegex().Replace(input, " $1");

    [GeneratedRegex(@"(?<=[a-z])([A-Z])")]
    private static partial Regex CamelCaseRegex();
}
