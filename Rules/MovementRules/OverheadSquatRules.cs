using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Rules.MovementRules;

public static class OverheadSquatRules
{
    public static string GetCompensationExplanation(MovementCompensation comp) => comp switch
    {
        MovementCompensation.KneesValgus => "Knees caving inward suggests weak glutes and tight adductors. This increases knee injury risk and limits squat effectiveness.",
        MovementCompensation.FeetTurnOut => "Feet turning out indicates tight calves and/or weak hip stabilizers. This can lead to ankle and knee issues over time.",
        MovementCompensation.FeetFlatten => "Feet flattening (pronation) suggests weak posterior tibialis and tight peroneals. This affects the entire kinetic chain.",
        MovementCompensation.KneesDominant => "Heels rising or knees pushing far forward indicates tight calves and ankle mobility limitations.",
        MovementCompensation.ExcessiveForwardLean => "Excessive forward lean points to tight hip flexors and/or weak core stability. This puts extra stress on the lower back.",
        MovementCompensation.LowBackArches => "Excessive low back arching suggests tight hip flexors and weak deep core muscles. This increases lumbar spine stress.",
        MovementCompensation.LowBackRounds => "Low back rounding indicates tight hamstrings and/or weak erector spinae. This is a common injury risk factor.",
        MovementCompensation.AsymmetricShift => "Weight shifting to one side suggests muscle imbalances or possible joint restrictions on the affected side.",
        MovementCompensation.ArmsForward => "Arms falling forward indicates tight lats and/or pecs with weak mid-back muscles. This affects overhead movements.",
        MovementCompensation.ArmsUneven => "Uneven arms suggest shoulder mobility asymmetry, possibly from previous injury or postural habits.",
        MovementCompensation.ShoulderElevation => "Shoulders elevating indicates overactive upper traps and weak lower traps/serratus anterior.",
        MovementCompensation.HipDrop => "Hip dropping on the non-standing side indicates weak gluteus medius. This is a key predictor of knee and ankle issues.",
        MovementCompensation.TrunkLateralLean => "Trunk leaning to one side suggests hip abductor weakness and/or lateral core instability.",
        MovementCompensation.AnklePronation => "Ankle pronation during single-leg stance indicates weak ankle stabilizers and potential hip weakness.",
        MovementCompensation.ExcessiveMovement => "Excessive wobbling or loss of balance indicates overall proprioceptive and stability deficits.",
        MovementCompensation.LimitedDepth => "Limited squat depth may indicate hip, ankle, or thoracic mobility restrictions.",
        MovementCompensation.PainReported => "Pain during movement requires attention. Your program will avoid aggravating exercises and focus on corrective work.",
        _ => "Compensation detected that may benefit from corrective exercise."
    };

    public static List<MuscleGroup> GetOveractiveMuscles(MovementCompensation comp) => comp switch
    {
        MovementCompensation.KneesValgus => new() { MuscleGroup.Adductors, MuscleGroup.TibialisAnterior },
        MovementCompensation.FeetTurnOut => new() { MuscleGroup.Calves, MuscleGroup.Peroneals },
        MovementCompensation.FeetFlatten => new() { MuscleGroup.Peroneals },
        MovementCompensation.KneesDominant => new() { MuscleGroup.Calves },
        MovementCompensation.ExcessiveForwardLean => new() { MuscleGroup.HipFlexors, MuscleGroup.Calves },
        MovementCompensation.LowBackArches => new() { MuscleGroup.HipFlexors, MuscleGroup.LowerBack },
        MovementCompensation.LowBackRounds => new() { MuscleGroup.Hamstrings },
        MovementCompensation.ArmsForward => new() { MuscleGroup.Lats, MuscleGroup.Chest },
        MovementCompensation.ShoulderElevation => new() { MuscleGroup.Shoulders },
        MovementCompensation.HipDrop => new() { MuscleGroup.Adductors },
        MovementCompensation.TrunkLateralLean => new() { MuscleGroup.Obliques },
        _ => new()
    };

    public static List<MuscleGroup> GetUnderactiveMuscles(MovementCompensation comp) => comp switch
    {
        MovementCompensation.KneesValgus => new() { MuscleGroup.Glutes, MuscleGroup.Abductors },
        MovementCompensation.FeetTurnOut => new() { MuscleGroup.Glutes, MuscleGroup.HipFlexors },
        MovementCompensation.FeetFlatten => new() { MuscleGroup.TibialisAnterior, MuscleGroup.Glutes },
        MovementCompensation.KneesDominant => new() { MuscleGroup.Glutes, MuscleGroup.Core },
        MovementCompensation.ExcessiveForwardLean => new() { MuscleGroup.Core, MuscleGroup.Glutes },
        MovementCompensation.LowBackArches => new() { MuscleGroup.Core, MuscleGroup.Glutes },
        MovementCompensation.LowBackRounds => new() { MuscleGroup.LowerBack, MuscleGroup.Core },
        MovementCompensation.ArmsForward => new() { MuscleGroup.UpperBack, MuscleGroup.RotatorCuff },
        MovementCompensation.ShoulderElevation => new() { MuscleGroup.UpperBack, MuscleGroup.RotatorCuff },
        MovementCompensation.HipDrop => new() { MuscleGroup.Glutes, MuscleGroup.Abductors },
        MovementCompensation.TrunkLateralLean => new() { MuscleGroup.Core, MuscleGroup.Glutes },
        MovementCompensation.AnklePronation => new() { MuscleGroup.TibialisAnterior, MuscleGroup.Glutes },
        MovementCompensation.ExcessiveMovement => new() { MuscleGroup.Core, MuscleGroup.Glutes },
        _ => new()
    };
}
