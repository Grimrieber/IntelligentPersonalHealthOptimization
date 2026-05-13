using IntelligentPersonalHealthOptimization.Models.Enums;
using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

[Table("Users")]
public class User
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [MaxLength(100), NotNull]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100), NotNull]
    public string LastName { get; set; } = string.Empty;

    public DateTime DateOfBirth { get; set; }

    public Gender Gender { get; set; }

    public double HeightCm { get; set; }

    public double WeightKg { get; set; }

    public ActivityLevel ActivityLevel { get; set; }

    public FitnessGoal FitnessGoal { get; set; }

    [MaxLength(1000)]
    public string MedicalConditions { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string InjuryAreas { get; set; } = string.Empty;

    [MaxLength(500)]
    public string MedicalNotes { get; set; } = string.Empty;

    [MaxLength(500)]
    public string InjuryHistory { get; set; } = string.Empty;

    public bool HasAcceptedDisclaimer { get; set; }

    public bool UseBiometricLogin { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
