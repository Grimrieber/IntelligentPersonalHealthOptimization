using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace IntelligentPersonalHealthOptimization.Helpers;

/// <summary>A tappable day-of-week chip used by the TrainingDaySelector control.
/// <see cref="IsSelected"/> drives both the chip styling and whether the day counts
/// toward the weekly training frequency. <see cref="ToggleCommand"/> is set by the parent
/// control so the chip's tap binds to its OWN property — avoiding the DataTemplate namescope
/// issues that break ancestor/x:Reference bindings inside a BindableLayout item.</summary>
public partial class DayToggle : ObservableObject
{
    public string Day { get; }          // canonical name, e.g. "Monday"
    public string Short { get; }        // chip label, e.g. "Mon"

    [ObservableProperty] private bool _isSelected;

    public ICommand? ToggleCommand { get; set; }

    public DayToggle(string day, string shortLabel)
    {
        Day = day;
        Short = shortLabel;
    }
}
