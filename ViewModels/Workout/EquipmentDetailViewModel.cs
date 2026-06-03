using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Constants;
using IntelligentPersonalHealthOptimization.Helpers;
using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.Workout;

[QueryProperty(nameof(SessionId), "sessionId")]
[QueryProperty(nameof(IsReentry), "reentry")]
public partial class EquipmentDetailViewModel : BaseViewModel
{
    private readonly IEquipmentIntelService _intelService;
    private readonly IUserService _userService;
    private int _userId;

    public EquipmentDetailViewModel(IEquipmentIntelService intelService, IUserService userService)
    {
        _intelService = intelService;
        _userService = userService;
        Title = "Your Equipment";
    }

    [ObservableProperty] private int _sessionId;
    [ObservableProperty] private bool _isReentry;

    [ObservableProperty] private PickerItem<TrainingLocation>? _selectedLocation;
    [ObservableProperty] private bool _showGymBranch;
    [ObservableProperty] private bool _showHomeBranch;
    [ObservableProperty] private bool _includeCardio;

    [ObservableProperty] private string _selectedChain = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DumbbellWeightKgDisplay))]
    [NotifyPropertyChangedFor(nameof(DumbbellWeightLbDisplay))]
    private double _dumbbellMaxKg = 22.5;

    [ObservableProperty] private bool _hasAdjustableDumbbells;

    public string DumbbellWeightKgDisplay => $"{DumbbellMaxKg:F1} kg";
    public string DumbbellWeightLbDisplay => $"{DumbbellMaxKg * 2.20462:F0} lb";

    public ObservableCollection<string> ChainOptions { get; } = [];
    public ObservableCollection<EquipmentSectionGroup> Sections { get; } = [];

    public List<PickerItem<TrainingLocation>> LocationOptions { get; } =
        PickerItem<TrainingLocation>.From([TrainingLocation.Gym, TrainingLocation.Home, TrainingLocation.Both]);

    public TrainingLocation TrainingLocation => SelectedLocation?.Value ?? TrainingLocation.Gym;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;
            _userId = user.Id;

            var ctx = await _intelService.LoadContextAsync(_userId);
            SelectedLocation = LocationOptions.FirstOrDefault(o => o.Value.Equals(ctx.TrainingLocation)) ?? LocationOptions[0];
            UpdateBranchVisibility();

            ChainOptions.Clear();
            foreach (var template in ctx.ChainTemplates)
                ChainOptions.Add(template.ChainName);
            ChainOptions.Add("Other / not listed");

            SelectedChain = ctx.ExistingEnvironment?.GymChain ?? string.Empty;
            IncludeCardio = ctx.ExistingEnvironment?.IncludeCardio ?? false;

            // Determine which items to pre-check: existing inventory wins, else legacy mapped
            var preChecked = ctx.ExistingInventory.Count > 0
                ? ctx.ExistingInventory.Select(i => i.ItemType).ToHashSet()
                : ctx.LegacyEquipmentMapped.ToHashSet();

            BuildSections(preChecked);

            // Pull existing DB max weight if any
            var existingDb = ctx.ExistingInventory.FirstOrDefault(i =>
                i.ItemType == EquipmentItemType.FixedDumbbells || i.ItemType == EquipmentItemType.AdjustableDumbbells);
            if (existingDb != null && existingDb.MaxLoadKg.HasValue)
            {
                DumbbellMaxKg = (double)existingDb.MaxLoadKg.Value;
                HasAdjustableDumbbells = existingDb.ItemType == EquipmentItemType.AdjustableDumbbells;
            }
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EquipmentDetail.Load", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    partial void OnSelectedLocationChanged(PickerItem<TrainingLocation>? value)
    {
        OnPropertyChanged(nameof(TrainingLocation));
        UpdateBranchVisibility();
        BuildSections(GetSelectedItems());
    }

    partial void OnIncludeCardioChanged(bool value)
    {
        // Rebuild to add/remove the Cardio section
        var current = GetSelectedItems();
        BuildSections(current);
    }

    [RelayCommand]
    private async Task ApplyChainPresetAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedChain) || SelectedChain == "Other / not listed")
            return;

        var template = await _intelService.GetChainTemplateByNameAsync(SelectedChain);
        if (template == null || string.IsNullOrWhiteSpace(template.TypicalEquipment)) return;

        var presetItems = template.TypicalEquipment
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => Enum.TryParse<EquipmentItemType>(s.Trim(), out var v) ? v : (EquipmentItemType?)null)
            .Where(v => v.HasValue)
            .Select(v => v!.Value)
            .ToHashSet();

        // Merge preset on top of current selection
        var current = GetSelectedItems();
        foreach (var item in presetItems) current.Add(item);

        BuildSections(current);

        await Shell.Current.DisplayAlert(
            $"{SelectedChain}",
            $"Pre-checked typical inventory for {SelectedChain}. Review and uncheck anything your location is missing." +
                (string.IsNullOrWhiteSpace(template.Restrictions) ? "" : $"\n\nNote: {template.Restrictions}"),
            "OK");
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        IsBusy = true;
        try
        {
            // Save inventory
            var selected = GetSelectedItems();
            var items = selected.Select(s => new EquipmentInventoryItem
            {
                ItemType = s,
                Location = TrainingLocation,
                MaxLoadKg = IsDumbbell(s) ? (decimal)DumbbellMaxKg : null
            }).ToList();

            // If they picked adjustable, swap fixed → adjustable
            if (HasAdjustableDumbbells)
            {
                var fixedDb = items.FirstOrDefault(i => i.ItemType == EquipmentItemType.FixedDumbbells);
                if (fixedDb != null)
                {
                    fixedDb.ItemType = EquipmentItemType.AdjustableDumbbells;
                }
                else if (!items.Any(i => i.ItemType == EquipmentItemType.AdjustableDumbbells))
                {
                    items.Add(new EquipmentInventoryItem
                    {
                        ItemType = EquipmentItemType.AdjustableDumbbells,
                        Location = TrainingLocation,
                        MaxLoadKg = (decimal)DumbbellMaxKg
                    });
                }
            }

            await _intelService.SaveInventoryAsync(_userId, items);

            // Next: Environment page if Home or Both, else straight to preview
            if (TrainingLocation == TrainingLocation.Home || TrainingLocation == TrainingLocation.Both)
            {
                await Shell.Current.GoToAsync($"{RouteConstants.EnvironmentDetail}?sessionId={SessionId}&reentry={IsReentry}");
            }
            else
            {
                // Save bare environment for gym chain & cardio flag
                await SaveBareEnvironmentAsync();
                await Shell.Current.GoToAsync($"{RouteConstants.ProgramPreview}?sessionId={SessionId}");
            }
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EquipmentDetail.Next", ex);
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BackAsync() => await Shell.Current.GoToAsync("..");

    private async Task SaveBareEnvironmentAsync()
    {
        var existing = await _intelService.GetEnvironmentAsync(_userId);
        var env = existing ?? new TrainingEnvironment { UserId = _userId };
        env.GymChain = SelectedChain == "Other / not listed" ? string.Empty : SelectedChain;
        env.IncludeCardio = IncludeCardio;
        await _intelService.SaveEnvironmentAsync(env);
    }

    private void UpdateBranchVisibility()
    {
        ShowGymBranch = TrainingLocation is TrainingLocation.Gym or TrainingLocation.Both;
        ShowHomeBranch = TrainingLocation is TrainingLocation.Home or TrainingLocation.Both;
    }

    private HashSet<EquipmentItemType> GetSelectedItems()
    {
        return Sections
            .SelectMany(s => s.Items)
            .Where(i => i.IsChecked)
            .Select(i => i.ItemType)
            .ToHashSet();
    }

    private void BuildSections(HashSet<EquipmentItemType> preChecked)
    {
        Sections.Clear();

        if (ShowGymBranch || ShowHomeBranch)
        {
            Sections.Add(MakeSection("Free Weights", [
                EquipmentItemType.FixedDumbbells,
                EquipmentItemType.AdjustableDumbbells,
                EquipmentItemType.BarbellOlympic,
                EquipmentItemType.Kettlebells
            ], preChecked));

            Sections.Add(MakeSection("Racks & Benches", [
                EquipmentItemType.PowerRack,
                EquipmentItemType.SquatStands,
                EquipmentItemType.SmithMachine,
                EquipmentItemType.FlatBench,
                EquipmentItemType.AdjustableBench
            ], preChecked));

            Sections.Add(MakeSection("Pulling & Suspended", [
                EquipmentItemType.PullUpBarMounted,
                EquipmentItemType.PullUpBarDoorway,
                EquipmentItemType.TrxSuspension,
                EquipmentItemType.DipStation,
                EquipmentItemType.GymnasticRings
            ], preChecked));
        }

        if (ShowGymBranch)
        {
            Sections.Add(MakeSection("Cable & Machines", [
                EquipmentItemType.CableColumn,
                EquipmentItemType.CableCrossover,
                EquipmentItemType.LatPulldown,
                EquipmentItemType.SeatedRowMachine,
                EquipmentItemType.LegPress,
                EquipmentItemType.HackSquat,
                EquipmentItemType.LegCurl,
                EquipmentItemType.LegExtension,
                EquipmentItemType.ChestPressMachine,
                EquipmentItemType.ShoulderPressMachine,
                EquipmentItemType.PecDeck,
                EquipmentItemType.HipThrustMachine,
                EquipmentItemType.HyperextensionGhd
            ], preChecked));
        }

        Sections.Add(MakeSection("Bands & Accessories", [
            EquipmentItemType.LoopBands,
            EquipmentItemType.TubeBandsHandles,
            EquipmentItemType.MiniBands,
            EquipmentItemType.FoamRoller,
            EquipmentItemType.YogaMat,
            EquipmentItemType.StabilityBall,
            EquipmentItemType.AbWheel,
            EquipmentItemType.Sliders
        ], preChecked));

        Sections.Add(MakeSection("Plyometric / Conditioning", [
            EquipmentItemType.PlyoBox,
            EquipmentItemType.JumpRope,
            EquipmentItemType.MedicineBall,
            EquipmentItemType.SlamBall,
            EquipmentItemType.BattleRopes,
            EquipmentItemType.Sled
        ], preChecked));

        if (IncludeCardio)
        {
            Sections.Add(MakeSection("Cardio", [
                EquipmentItemType.Treadmill,
                EquipmentItemType.StationaryBike,
                EquipmentItemType.AirBike,
                EquipmentItemType.RowingMachine,
                EquipmentItemType.Elliptical,
                EquipmentItemType.StairClimber,
                EquipmentItemType.SkiErg
            ], preChecked));
        }
    }

    private static EquipmentSectionGroup MakeSection(string name, EquipmentItemType[] items, HashSet<EquipmentItemType> preChecked)
    {
        return new EquipmentSectionGroup
        {
            Name = name,
            Items = new ObservableCollection<EquipmentItemToggle>(
                items.Select(i => new EquipmentItemToggle
                {
                    ItemType = i,
                    DisplayName = HumanizeItem(i),
                    IsChecked = preChecked.Contains(i)
                }))
        };
    }

    private static bool IsDumbbell(EquipmentItemType t) =>
        t == EquipmentItemType.FixedDumbbells || t == EquipmentItemType.AdjustableDumbbells;

    private static string HumanizeItem(EquipmentItemType t) => t switch
    {
        EquipmentItemType.AdjustableDumbbells => "Adjustable dumbbells",
        EquipmentItemType.FixedDumbbells => "Fixed dumbbells",
        EquipmentItemType.BarbellOlympic => "Olympic barbell (45 lb)",
        EquipmentItemType.BarbellStandard => "Standard barbell (1\")",
        EquipmentItemType.BarbellFixed => "Fixed/preloaded barbells",
        EquipmentItemType.PowerRack => "Power rack / cage",
        EquipmentItemType.SquatStands => "Squat stands",
        EquipmentItemType.SmithMachine => "Smith machine",
        EquipmentItemType.FlatBench => "Flat bench",
        EquipmentItemType.AdjustableBench => "Adjustable bench",
        EquipmentItemType.PullUpBarMounted => "Pull-up bar (mounted)",
        EquipmentItemType.PullUpBarDoorway => "Pull-up bar (doorway)",
        EquipmentItemType.TrxSuspension => "TRX / suspension trainer",
        EquipmentItemType.DipStation => "Dip station",
        EquipmentItemType.GymnasticRings => "Gymnastic rings",
        EquipmentItemType.CableColumn => "Cable column",
        EquipmentItemType.CableCrossover => "Cable crossover",
        EquipmentItemType.LatPulldown => "Lat pulldown",
        EquipmentItemType.SeatedRowMachine => "Seated row machine",
        EquipmentItemType.LegPress => "Leg press",
        EquipmentItemType.HackSquat => "Hack squat",
        EquipmentItemType.LegCurl => "Leg curl",
        EquipmentItemType.LegExtension => "Leg extension",
        EquipmentItemType.ChestPressMachine => "Chest press machine",
        EquipmentItemType.ShoulderPressMachine => "Shoulder press machine",
        EquipmentItemType.PecDeck => "Pec deck / fly machine",
        EquipmentItemType.HipThrustMachine => "Hip thrust machine",
        EquipmentItemType.HyperextensionGhd => "Hyperextension / GHD",
        EquipmentItemType.LoopBands => "Resistance bands (loop)",
        EquipmentItemType.TubeBandsHandles => "Resistance bands (tube w/ handles)",
        EquipmentItemType.MiniBands => "Mini-bands",
        EquipmentItemType.FoamRoller => "Foam roller",
        EquipmentItemType.YogaMat => "Yoga mat",
        EquipmentItemType.StabilityBall => "Stability ball",
        EquipmentItemType.AbWheel => "Ab wheel",
        EquipmentItemType.Sliders => "Sliders",
        EquipmentItemType.PlyoBox => "Plyo box",
        EquipmentItemType.JumpRope => "Jump rope",
        EquipmentItemType.MedicineBall => "Medicine ball",
        EquipmentItemType.SlamBall => "Slam ball",
        EquipmentItemType.BattleRopes => "Battle ropes",
        EquipmentItemType.Sled => "Sled",
        EquipmentItemType.Kettlebells => "Kettlebells",
        EquipmentItemType.Treadmill => "Treadmill",
        EquipmentItemType.StationaryBike => "Stationary bike",
        EquipmentItemType.AirBike => "Air bike",
        EquipmentItemType.RowingMachine => "Rowing machine",
        EquipmentItemType.Elliptical => "Elliptical",
        EquipmentItemType.StairClimber => "Stair climber",
        EquipmentItemType.SkiErg => "Ski erg",
        _ => t.ToString()
    };
}

public class EquipmentSectionGroup
{
    public string Name { get; set; } = string.Empty;
    public ObservableCollection<EquipmentItemToggle> Items { get; set; } = [];
}

public partial class EquipmentItemToggle : ObservableObject
{
    public EquipmentItemType ItemType { get; set; }
    public string DisplayName { get; set; } = string.Empty;

    [ObservableProperty]
    private bool _isChecked;
}
