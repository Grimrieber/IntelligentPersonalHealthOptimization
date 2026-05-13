using SQLite;

namespace IntelligentPersonalHealthOptimization.Models;

/// <summary>
/// Stores a complete CES assessment result in SQLite.
/// Links to the existing AssessmentSession for overhead squat and single-leg data.
/// </summary>
public class CesAssessment
{
    [PrimaryKey, AutoIncrement]
    public int Id { get; set; }

    [Indexed]
    public int UserId { get; set; }

    [Indexed]
    public int AssessmentSessionId { get; set; }

    public DateTime AssessmentDate { get; set; } = DateTime.UtcNow;

    // ===== Static Posture — Anterior (Front) View =====
    // Each field stores CompensationSeverity (0-3)
    public int FeetTurnedOut { get; set; }
    public int FeetPronated { get; set; }
    public int FeetSupinated { get; set; }
    public int KneesValgus { get; set; }
    public int KneesVarus { get; set; }
    public int UnevenHips { get; set; }
    public int UnevenShoulders { get; set; }
    public int HeadTilt { get; set; }

    // ===== Static Posture — Lateral (Side) View =====
    public int ForwardHead { get; set; }
    public int RoundedShoulders { get; set; }
    public int ThoracicKyphosis { get; set; }
    public int LumbarLordosis { get; set; }
    public int FlatBack { get; set; }
    public int AnteriorPelvicTilt { get; set; }
    public int PosteriorPelvicTilt { get; set; }
    public int KneeHyperextension { get; set; }

    // ===== Static Posture — Posterior (Back) View =====
    public int ScapularWinging { get; set; }
    public int SpinalDeviation { get; set; }
    public int CalcanealEversion { get; set; }

    // ===== Overhead Squat Compensations (severity 0-3) =====
    public int OhsFeetTurnOut { get; set; }
    public int OhsFeetFlatten { get; set; }
    public int OhsKneesValgus { get; set; }
    public int OhsForwardLean { get; set; }
    public int OhsLowBackArch { get; set; }
    public int OhsLowBackRound { get; set; }
    public int OhsHeelsRise { get; set; }
    public int OhsArmsFallForward { get; set; }
    public int OhsAsymmetricShift { get; set; }

    // ===== Single-Leg Squat — Left =====
    public int SlsLeftKneeValgus { get; set; }
    public int SlsLeftHipDrop { get; set; }
    public int SlsLeftTrunkLean { get; set; }
    public int SlsLeftFootPronation { get; set; }

    // ===== Single-Leg Squat — Right =====
    public int SlsRightKneeValgus { get; set; }
    public int SlsRightHipDrop { get; set; }
    public int SlsRightTrunkLean { get; set; }
    public int SlsRightFootPronation { get; set; }

    // ===== Push Assessment =====
    public int PushScapularWinging { get; set; }
    public int PushShoulderHiking { get; set; }
    public int PushForwardHeadPoke { get; set; }
    public int PushLowBackSag { get; set; }

    // ===== Pull Assessment =====
    public int PullShoulderHiking { get; set; }
    public int PullHeadProtrusion { get; set; }
    public int PullLowBackExtension { get; set; }

    // ===== Computed Scores =====
    public int TotalCompensationScore { get; set; }     // Sum of all severity scores
    public int OverallMovementScore { get; set; }       // 0-100 (higher = better)
    public int RiskLevel { get; set; }                  // CesRiskLevel enum value

    // ===== Identified Syndromes (stored as comma-separated) =====
    [MaxLength(500)]
    public string IdentifiedSyndromes { get; set; } = string.Empty;

    // ===== Pain Reported =====
    public bool PainDuringAssessment { get; set; }

    [MaxLength(500)]
    public string PainLocation { get; set; } = string.Empty;

    // ===== Objective Measurements =====
    public double AnkleDorsiflexionLeftInches { get; set; }
    public double AnkleDorsiflexionRightInches { get; set; }
    public int SingleLegBalanceLeftSeconds { get; set; }
    public int SingleLegBalanceRightSeconds { get; set; }
    public bool WallAngelFullContact { get; set; }

    // ===== Generated Program Reference =====
    public int GeneratedProgramId { get; set; }
}
