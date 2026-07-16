using CommunityToolkit.Mvvm.ComponentModel;

namespace IntelligentPersonalHealthOptimization.Models;

/// <summary>
/// A selectable chip for inline multi-select UIs (tap to select/deselect).
/// Replaces the old "+ Add via picker → removable pill with a tiny ✕" pattern.
/// <see cref="Value"/> carries the underlying option (enum or string); the VM
/// syncs the real selection collection when a chip toggles.
/// </summary>
public partial class ToggleChip : ObservableObject
{
    public object? Value { get; init; }
    public string Label { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}
