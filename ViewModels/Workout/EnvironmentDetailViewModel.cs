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
public partial class EnvironmentDetailViewModel : BaseViewModel
{
    private readonly IEquipmentIntelService _intelService;
    private readonly IUserService _userService;
    private int _userId;
    private string _gymChainFromPrev = string.Empty;
    private bool _includeCardioFromPrev;

    public EnvironmentDetailViewModel(IEquipmentIntelService intelService, IUserService userService)
    {
        _intelService = intelService;
        _userService = userService;
        Title = "Your Environment";
    }

    [ObservableProperty] private int _sessionId;
    [ObservableProperty] private bool _isReentry;

    [ObservableProperty] private PickerItem<CeilingHeight>? _selectedCeiling;
    [ObservableProperty] private PickerItem<NoiseTolerance>? _selectedNoise;
    [ObservableProperty] private PickerItem<FloorSpace>? _selectedFloorSpace;
    [ObservableProperty] private bool _hasOutdoorAccess;

    public List<PickerItem<CeilingHeight>> CeilingOptions { get; } =
        PickerItem<CeilingHeight>.From([CeilingHeight.Low, CeilingHeight.Standard, CeilingHeight.High]);

    public List<PickerItem<NoiseTolerance>> NoiseOptions { get; } =
        PickerItem<NoiseTolerance>.From([NoiseTolerance.Quiet, NoiseTolerance.Moderate, NoiseTolerance.Loud]);

    public List<PickerItem<FloorSpace>> FloorSpaceOptions { get; } =
        PickerItem<FloorSpace>.From([FloorSpace.Tight, FloorSpace.Moderate, FloorSpace.Open]);

    public CeilingHeight Ceiling => SelectedCeiling?.Value ?? CeilingHeight.Standard;
    public NoiseTolerance Noise => SelectedNoise?.Value ?? NoiseTolerance.Moderate;
    public FloorSpace FloorSpace => SelectedFloorSpace?.Value ?? FloorSpace.Moderate;

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var user = await _userService.GetCurrentUserAsync();
            if (user == null) return;
            _userId = user.Id;

            var existing = await _intelService.GetEnvironmentAsync(_userId);

            var initialCeiling = existing?.HomeCeiling is null or CeilingHeight.NotApplicable
                ? CeilingHeight.Standard : existing.HomeCeiling;
            var initialNoise = existing?.HomeNoise is null or NoiseTolerance.NotApplicable
                ? NoiseTolerance.Moderate : existing.HomeNoise;
            var initialFloor = existing?.HomeFloorSpace is null or FloorSpace.NotApplicable
                ? FloorSpace.Moderate : existing.HomeFloorSpace;

            SelectedCeiling = CeilingOptions.FirstOrDefault(o => o.Value == initialCeiling) ?? CeilingOptions[1];
            SelectedNoise = NoiseOptions.FirstOrDefault(o => o.Value == initialNoise) ?? NoiseOptions[1];
            SelectedFloorSpace = FloorSpaceOptions.FirstOrDefault(o => o.Value == initialFloor) ?? FloorSpaceOptions[1];

            if (existing != null)
            {
                HasOutdoorAccess = existing.HasOutdoorAccess;
                _gymChainFromPrev = existing.GymChain;
                _includeCardioFromPrev = existing.IncludeCardio;
            }
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EnvironmentDetail.Load", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task NextAsync()
    {
        IsBusy = true;
        try
        {
            var existing = await _intelService.GetEnvironmentAsync(_userId);
            var env = existing ?? new TrainingEnvironment { UserId = _userId };

            env.HomeCeiling = Ceiling;
            env.HomeNoise = Noise;
            env.HomeFloorSpace = FloorSpace;
            env.HasOutdoorAccess = HasOutdoorAccess;
            // Preserve gym chain & cardio set on previous page
            if (existing == null)
            {
                env.GymChain = _gymChainFromPrev;
                env.IncludeCardio = _includeCardioFromPrev;
            }

            await _intelService.SaveEnvironmentAsync(env);

            await Shell.Current.GoToAsync($"{RouteConstants.ProgramPreview}?sessionId={SessionId}");
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("EnvironmentDetail.Next", ex);
            await Shell.Current.DisplayAlert("Error", ex.Message, "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task BackAsync() => await Shell.Current.GoToAsync("..");
}
