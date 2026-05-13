namespace IntelligentPersonalHealthOptimization.Models.Enums;

public enum MovementCompensation
{
    // Overhead Squat - Foot/Ankle
    FeetTurnOut,
    FeetFlatten,

    // Overhead Squat - Knee
    KneesValgus,
    KneesDominant,

    // Overhead Squat - Hip/Pelvis (LPHC)
    ExcessiveForwardLean,
    LowBackArches,
    LowBackRounds,
    AsymmetricShift,

    // Overhead Squat - Upper Body
    ArmsForward,
    ArmsUneven,
    ShoulderElevation,

    // Single-Leg Balance
    HipDrop,
    TrunkLateralLean,
    AnklePronation,
    ExcessiveMovement,

    // General
    LimitedDepth,
    PainReported
}
