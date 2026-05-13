using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Rules.PostureRules;

public static class PostureAnalysisRules
{
    public static string GetDeviationExplanation(PostureDeviation deviation)
    {
        return deviation switch
        {
            PostureDeviation.ForwardHead => "Forward head posture indicates tight neck extensors and weak deep neck flexors. This can lead to neck pain and headaches.",
            PostureDeviation.RoundedShoulders => "Rounded shoulders suggest tight pecs and anterior deltoids with weak mid-back muscles. This limits overhead mobility and increases shoulder injury risk.",
            PostureDeviation.Kyphosis => "Excessive upper back rounding (kyphosis) indicates tight chest muscles and weak thoracic extensors. This compresses the ribcage and can affect breathing.",
            PostureDeviation.Lordosis => "Excessive lower back arch (lordosis) suggests tight hip flexors and lower back with weak abdominals and glutes. This increases disc compression.",
            PostureDeviation.FlatBack => "Flat back posture indicates weak hip flexors and tight hamstrings with posterior pelvic tilt. This reduces shock absorption during activity.",
            PostureDeviation.Swayback => "Swayback posture shows the pelvis pushed forward with tight hamstrings and hip extensors. The upper body leans back to compensate.",
            PostureDeviation.UnevenShoulders => "Uneven shoulders may indicate muscular imbalance, scoliosis, or habitual posture patterns. One side is working harder than the other.",
            PostureDeviation.UnevenHips => "Uneven hips suggest leg length discrepancy or muscular imbalance in the pelvis. This affects gait and can cause compensatory pain.",
            PostureDeviation.KneesHyperextended => "Hyperextended knees indicate weak quadriceps and tight calves. This stresses the knee joint and can lead to knee pain.",
            PostureDeviation.FeetPronated => "Flat or pronated feet suggest weak foot intrinsic muscles and tight calves. This affects the entire kinetic chain from ankles to hips.",
            PostureDeviation.FeetSupinated => "Supinated (high-arch) feet reduce shock absorption. This can lead to lateral ankle sprains and stress on the outer leg.",
            PostureDeviation.AnteriorPelvicTilt => "Anterior pelvic tilt indicates tight hip flexors and lower back with weak glutes and abdominals. This is very common in people who sit a lot.",
            PostureDeviation.PosteriorPelvicTilt => "Posterior pelvic tilt shows tight hamstrings and abdominals with weak hip flexors and lower back. This flattens the natural lumbar curve.",
            _ => "Postural deviation detected."
        };
    }

    public static List<MuscleGroup> GetOveractiveMuscles(PostureDeviation deviation)
    {
        return deviation switch
        {
            PostureDeviation.ForwardHead => [MuscleGroup.UpperBack],
            PostureDeviation.RoundedShoulders => [MuscleGroup.Chest, MuscleGroup.Shoulders],
            PostureDeviation.Kyphosis => [MuscleGroup.Chest, MuscleGroup.Shoulders],
            PostureDeviation.Lordosis => [MuscleGroup.HipFlexors, MuscleGroup.LowerBack],
            PostureDeviation.FlatBack => [MuscleGroup.Hamstrings, MuscleGroup.Core],
            PostureDeviation.Swayback => [MuscleGroup.Hamstrings, MuscleGroup.LowerBack],
            PostureDeviation.KneesHyperextended => [MuscleGroup.Calves],
            PostureDeviation.FeetPronated => [MuscleGroup.Calves, MuscleGroup.Peroneals],
            PostureDeviation.FeetSupinated => [MuscleGroup.TibialisAnterior],
            PostureDeviation.AnteriorPelvicTilt => [MuscleGroup.HipFlexors, MuscleGroup.LowerBack],
            PostureDeviation.PosteriorPelvicTilt => [MuscleGroup.Hamstrings, MuscleGroup.Core],
            _ => []
        };
    }

    public static List<MuscleGroup> GetUnderactiveMuscles(PostureDeviation deviation)
    {
        return deviation switch
        {
            PostureDeviation.ForwardHead => [MuscleGroup.Core],
            PostureDeviation.RoundedShoulders => [MuscleGroup.UpperBack, MuscleGroup.RotatorCuff],
            PostureDeviation.Kyphosis => [MuscleGroup.UpperBack, MuscleGroup.LowerBack],
            PostureDeviation.Lordosis => [MuscleGroup.Core, MuscleGroup.Glutes],
            PostureDeviation.FlatBack => [MuscleGroup.HipFlexors, MuscleGroup.LowerBack],
            PostureDeviation.Swayback => [MuscleGroup.Core, MuscleGroup.HipFlexors],
            PostureDeviation.KneesHyperextended => [MuscleGroup.Quadriceps, MuscleGroup.Glutes],
            PostureDeviation.FeetPronated => [MuscleGroup.TibialisAnterior, MuscleGroup.Glutes],
            PostureDeviation.FeetSupinated => [MuscleGroup.Peroneals, MuscleGroup.Calves],
            PostureDeviation.AnteriorPelvicTilt => [MuscleGroup.Core, MuscleGroup.Glutes],
            PostureDeviation.PosteriorPelvicTilt => [MuscleGroup.HipFlexors, MuscleGroup.LowerBack],
            _ => []
        };
    }

    /// <summary>
    /// Maps posture deviations to equivalent movement compensations for corrective exercise selection.
    /// </summary>
    public static List<MovementCompensation> MapToCompensations(PostureDeviation deviation)
    {
        return deviation switch
        {
            PostureDeviation.ForwardHead => [MovementCompensation.ShoulderElevation],
            PostureDeviation.RoundedShoulders => [MovementCompensation.ArmsForward, MovementCompensation.ShoulderElevation],
            PostureDeviation.Kyphosis => [MovementCompensation.ArmsForward, MovementCompensation.ExcessiveForwardLean],
            PostureDeviation.Lordosis => [MovementCompensation.LowBackArches],
            PostureDeviation.FlatBack => [MovementCompensation.LowBackRounds],
            PostureDeviation.Swayback => [MovementCompensation.LowBackArches, MovementCompensation.ExcessiveForwardLean],
            PostureDeviation.KneesHyperextended => [MovementCompensation.KneesDominant],
            PostureDeviation.FeetPronated => [MovementCompensation.FeetFlatten, MovementCompensation.AnklePronation],
            PostureDeviation.FeetSupinated => [MovementCompensation.FeetTurnOut],
            PostureDeviation.AnteriorPelvicTilt => [MovementCompensation.LowBackArches, MovementCompensation.ExcessiveForwardLean],
            PostureDeviation.PosteriorPelvicTilt => [MovementCompensation.LowBackRounds],
            _ => []
        };
    }
}
