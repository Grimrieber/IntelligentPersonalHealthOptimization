using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Implementation;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.ViewModels.CesAssessment;

public partial class CesResultViewModel : CesStepViewModelBase
{
    public CesResultViewModel(ICesAssessmentCoordinator coordinator) : base(coordinator)
    {
        Title = "Your Results";
    }

    [ObservableProperty] private int _movementScore;
    [ObservableProperty] private string _scoreDescription = string.Empty;
    [ObservableProperty] private string _riskLevelText = string.Empty;
    [ObservableProperty] private Color _riskColor = Colors.Green;
    [ObservableProperty] private bool _hasSyndromes;
    [ObservableProperty] private bool _showReferral;
    [ObservableProperty] private ObservableCollection<SyndromeDisplayItem> _syndromes = new();

    [RelayCommand]
    private Task LoadResultsAsync()
    {
        try
        {
            UpdateProgress();

            // Calculate scores
            Data.TotalCompensationScore = CesSyndromeEngine.CalculateTotalScore(Data);
            MovementScore = CesSyndromeEngine.CalculateMovementScore(Data.TotalCompensationScore);
            ScoreDescription = CesSyndromeEngine.GetScoreDescription(MovementScore);

            // Identify syndromes
            var identified = CesSyndromeEngine.IdentifySyndromes(Data);
            HasSyndromes = identified.Count > 0;

            // Determine risk
            var riskLevel = CesSyndromeEngine.CalculateRiskLevel(Data, identified);
            ShowReferral = riskLevel == CesRiskLevel.Referral;
            RiskLevelText = riskLevel switch
            {
                CesRiskLevel.Low => "Low Risk",
                CesRiskLevel.Moderate => "Moderate Risk",
                CesRiskLevel.High => "High Risk",
                CesRiskLevel.Referral => "Professional Evaluation Recommended",
                _ => "Unknown"
            };
            RiskColor = riskLevel switch
            {
                CesRiskLevel.Low => Color.FromArgb("#4CAF50"),
                CesRiskLevel.Moderate => Color.FromArgb("#FF9800"),
                CesRiskLevel.High => Color.FromArgb("#F44336"),
                CesRiskLevel.Referral => Color.FromArgb("#D32F2F"),
                _ => Colors.Gray
            };

            // Build syndrome display items
            Syndromes.Clear();
            foreach (var syndrome in identified)
            {
                var (overactive, underactive) = CesSyndromeEngine.GetMuscleImbalances(syndrome);
                Syndromes.Add(new SyndromeDisplayItem
                {
                    Name = CesSyndromeEngine.GetSyndromeName(syndrome),
                    Description = CesSyndromeEngine.GetSyndromeDescription(syndrome),
                    DailyImpact = CesSyndromeEngine.GetDailyImpact(syndrome),
                    OveractiveMuscles = string.Join(", ", overactive),
                    UnderactiveMuscles = string.Join(", ", underactive)
                });
            }

            if (!HasSyndromes)
            {
                Syndromes.Add(new SyndromeDisplayItem
                {
                    Name = "No Major Syndromes Detected",
                    Description = "Your movement patterns look good! Minor compensations may still benefit from corrective work during warm-ups.",
                    DailyImpact = string.Empty,
                    OveractiveMuscles = string.Empty,
                    UnderactiveMuscles = string.Empty
                });
            }
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("CesResult.LoadResults", ex);
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task CompleteAsync()
    {
        IsBusy = true;
        try
        {
            await Coordinator.CompleteAssessmentAsync();
        }
        catch (Exception ex)
        {
            Services.CrashLogger.Log("CesResult.Complete", ex);
            await Shell.Current.DisplayAlert("Error", "Could not save your assessment. Please try again.", "OK");
        }
        finally
        {
            IsBusy = false;
        }
    }
}

public class SyndromeDisplayItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DailyImpact { get; set; } = string.Empty;
    public string OveractiveMuscles { get; set; } = string.Empty;
    public string UnderactiveMuscles { get; set; } = string.Empty;
    public bool HasMuscleInfo => !string.IsNullOrEmpty(OveractiveMuscles);
    public bool HasDailyImpact => !string.IsNullOrEmpty(DailyImpact);
}
