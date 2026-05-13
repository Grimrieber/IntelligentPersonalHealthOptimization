using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

/// <summary>
/// Identifies distortion syndromes from CES assessment data,
/// calculates risk levels, and generates corrective exercise prescriptions
/// following the Corrective Exercise Continuum (Inhibit → Lengthen → Activate → Integrate).
/// </summary>
public static class CesSyndromeEngine
{
    /// <summary>
    /// Analyze a CES assessment and identify all distortion syndromes present.
    /// </summary>
    public static List<DistortionSyndrome> IdentifySyndromes(CesAssessment a)
    {
        var syndromes = new List<DistortionSyndrome>();

        // Upper Crossed: Forward head + rounded shoulders + thoracic kyphosis
        if (a.ForwardHead >= 2 || a.RoundedShoulders >= 2 || a.ThoracicKyphosis >= 2 ||
            (a.ForwardHead >= 1 && a.RoundedShoulders >= 1) ||
            (a.OhsArmsFallForward >= 2 && a.ForwardHead >= 1))
            syndromes.Add(DistortionSyndrome.UpperCrossed);

        // Lower Crossed: Anterior pelvic tilt + lumbar lordosis + hip flexor dominance
        if (a.AnteriorPelvicTilt >= 2 || a.LumbarLordosis >= 2 ||
            (a.AnteriorPelvicTilt >= 1 && a.OhsLowBackArch >= 1) ||
            (a.OhsLowBackArch >= 2 && a.OhsForwardLean >= 1))
            syndromes.Add(DistortionSyndrome.LowerCrossed);

        // Pronation Distortion: Flat feet + knee valgus
        if (a.FeetPronated >= 2 || a.KneesValgus >= 2 ||
            (a.FeetPronated >= 1 && a.KneesValgus >= 1) ||
            (a.OhsFeetFlatten >= 2 || a.OhsKneesValgus >= 2) ||
            (a.CalcanealEversion >= 2))
            syndromes.Add(DistortionSyndrome.PronationDistortion);

        // Posterior Pelvic Tilt
        if (a.PosteriorPelvicTilt >= 2 || a.FlatBack >= 2 ||
            (a.PosteriorPelvicTilt >= 1 && a.FlatBack >= 1))
            syndromes.Add(DistortionSyndrome.PosteriorPelvicTilt);

        // Knee Hyperextension
        if (a.KneeHyperextension >= 2)
            syndromes.Add(DistortionSyndrome.KneeHyperextension);

        // Knee Varus
        if (a.KneesVarus >= 2)
            syndromes.Add(DistortionSyndrome.KneeVarus);

        // Foot Supination
        if (a.FeetSupinated >= 2)
            syndromes.Add(DistortionSyndrome.FootSupination);

        // Spinal Asymmetry
        if (a.SpinalDeviation >= 2 || a.UnevenHips >= 2 ||
            (a.SpinalDeviation >= 1 && a.UnevenHips >= 1 && a.UnevenShoulders >= 1))
            syndromes.Add(DistortionSyndrome.SpinalAsymmetry);

        return syndromes;
    }

    /// <summary>
    /// Calculate total compensation score from a CES assessment.
    /// </summary>
    public static int CalculateTotalScore(CesAssessment a)
    {
        return a.FeetTurnedOut + a.FeetPronated + a.FeetSupinated + a.KneesValgus + a.KneesVarus +
               a.UnevenHips + a.UnevenShoulders + a.HeadTilt +
               a.ForwardHead + a.RoundedShoulders + a.ThoracicKyphosis + a.LumbarLordosis +
               a.FlatBack + a.AnteriorPelvicTilt + a.PosteriorPelvicTilt + a.KneeHyperextension +
               a.ScapularWinging + a.SpinalDeviation + a.CalcanealEversion +
               a.OhsFeetTurnOut + a.OhsFeetFlatten + a.OhsKneesValgus + a.OhsForwardLean +
               a.OhsLowBackArch + a.OhsLowBackRound + a.OhsHeelsRise + a.OhsArmsFallForward +
               a.OhsAsymmetricShift +
               a.SlsLeftKneeValgus + a.SlsLeftHipDrop + a.SlsLeftTrunkLean + a.SlsLeftFootPronation +
               a.SlsRightKneeValgus + a.SlsRightHipDrop + a.SlsRightTrunkLean + a.SlsRightFootPronation +
               a.PushScapularWinging + a.PushShoulderHiking + a.PushForwardHeadPoke + a.PushLowBackSag +
               a.PullShoulderHiking + a.PullHeadProtrusion + a.PullLowBackExtension;
    }

    /// <summary>
    /// Convert total score to 0-100 movement quality score (higher = better).
    /// </summary>
    public static int CalculateMovementScore(int totalCompensationScore)
    {
        // Max possible score: ~42 fields * 3 severity = 126
        const int maxPossible = 126;
        var normalized = Math.Max(0, 100 - (int)(totalCompensationScore / (double)maxPossible * 100));
        return Math.Clamp(normalized, 0, 100);
    }

    /// <summary>
    /// Determine CES risk level from assessment data.
    /// </summary>
    public static CesRiskLevel CalculateRiskLevel(CesAssessment a, List<DistortionSyndrome> syndromes)
    {
        if (a.PainDuringAssessment)
            return CesRiskLevel.Referral;

        var totalScore = a.TotalCompensationScore;

        if (syndromes.Count >= 3 || totalScore >= 40)
            return CesRiskLevel.High;

        if (syndromes.Count >= 2 || totalScore >= 20)
            return CesRiskLevel.Moderate;

        if (syndromes.Count >= 1 || totalScore >= 8)
            return CesRiskLevel.Low;

        return CesRiskLevel.Low;
    }

    /// <summary>
    /// Get a user-friendly name for a syndrome.
    /// </summary>
    public static string GetSyndromeName(DistortionSyndrome syndrome) => syndrome switch
    {
        DistortionSyndrome.UpperCrossed => "Upper Crossed Syndrome",
        DistortionSyndrome.LowerCrossed => "Lower Crossed Syndrome",
        DistortionSyndrome.PronationDistortion => "Pronation Distortion Syndrome",
        DistortionSyndrome.PosteriorPelvicTilt => "Posterior Pelvic Tilt",
        DistortionSyndrome.KneeHyperextension => "Knee Hyperextension",
        DistortionSyndrome.KneeVarus => "Knee Varus (Bow-Legged)",
        DistortionSyndrome.FootSupination => "Foot Supination (High Arch)",
        DistortionSyndrome.SpinalAsymmetry => "Spinal Asymmetry",
        _ => "None Identified"
    };

    /// <summary>
    /// Get the description of what the syndrome means and its consequences.
    /// </summary>
    public static string GetSyndromeDescription(DistortionSyndrome syndrome) => syndrome switch
    {
        DistortionSyndrome.UpperCrossed =>
            "A pattern of tight chest and neck muscles with weak upper back and deep neck muscles. " +
            "This causes forward head posture, rounded shoulders, and upper back rounding. " +
            "Can lead to tension headaches, neck pain, shoulder impingement, and reduced overhead mobility.",

        DistortionSyndrome.LowerCrossed =>
            "A pattern of tight hip flexors and low back muscles with weak glutes and core. " +
            "This causes an exaggerated low back arch and anterior pelvic tilt. " +
            "Can lead to chronic low back pain, hip impingement, hamstring strains, and disc herniation.",

        DistortionSyndrome.PronationDistortion =>
            "A pattern of flat feet, knee collapse inward, and internal shin rotation. " +
            "Can lead to plantar fasciitis, shin splints, IT band syndrome, knee pain, and increased ACL injury risk.",

        DistortionSyndrome.PosteriorPelvicTilt =>
            "A flat back with reduced lumbar curve. Tight hamstrings and glutes with weak hip flexors and low back. " +
            "Can lead to lumbar disc degeneration and reduced athletic power.",

        DistortionSyndrome.KneeHyperextension =>
            "Knees push backward past neutral when standing. Tight calves and quadriceps with weak hamstrings. " +
            "Can lead to posterior knee pain, ligament laxity, and meniscal damage.",

        DistortionSyndrome.KneeVarus =>
            "Knees angle outward (bow-legged). Tight external rotators with weak adductors. " +
            "Can lead to lateral knee pain, IT band syndrome, and lateral meniscus wear.",

        DistortionSyndrome.FootSupination =>
            "Excessively high arch with weight on the outer foot. Weak peroneals with tight tibialis posterior. " +
            "Can lead to ankle sprains, stress fractures, and lateral foot pain.",

        DistortionSyndrome.SpinalAsymmetry =>
            "A lateral curve in the spine with trunk asymmetry. " +
            "Can lead to unilateral back pain, hip dysfunction, and compensatory patterns throughout the body.",

        _ => string.Empty
    };

    /// <summary>
    /// Get the overactive and underactive muscles for a syndrome.
    /// </summary>
    public static (string[] Overactive, string[] Underactive) GetMuscleImbalances(DistortionSyndrome syndrome) => syndrome switch
    {
        DistortionSyndrome.UpperCrossed => (
            new[] { "Upper Trapezius", "Levator Scapulae", "Pectoralis Major/Minor", "SCM", "Suboccipitals", "Latissimus Dorsi" },
            new[] { "Deep Cervical Flexors", "Mid Trapezius", "Lower Trapezius", "Rhomboids", "Serratus Anterior", "Rotator Cuff" }
        ),
        DistortionSyndrome.LowerCrossed => (
            new[] { "Hip Flexors (Psoas, Rectus Femoris)", "TFL", "Erector Spinae (Lumbar)", "Adductors" },
            new[] { "Gluteus Maximus", "Gluteus Medius", "Deep Core (TVA, Multifidus)", "Rectus Abdominis", "Hamstrings" }
        ),
        DistortionSyndrome.PronationDistortion => (
            new[] { "Peroneals", "Lateral Gastrocnemius", "Adductors", "TFL/IT Band", "Biceps Femoris" },
            new[] { "Tibialis Posterior", "Gluteus Medius", "Gluteus Maximus", "VMO (Inner Quad)", "Intrinsic Foot Muscles" }
        ),
        DistortionSyndrome.PosteriorPelvicTilt => (
            new[] { "Hamstrings", "Gluteus Maximus", "Rectus Abdominis" },
            new[] { "Hip Flexors (Iliopsoas)", "Erector Spinae (Lumbar)" }
        ),
        DistortionSyndrome.KneeHyperextension => (
            new[] { "Soleus", "Gastrocnemius", "Quadriceps" },
            new[] { "Hamstrings", "Popliteus" }
        ),
        DistortionSyndrome.KneeVarus => (
            new[] { "Piriformis", "Biceps Femoris", "TFL" },
            new[] { "Adductors", "Medial Hamstring", "Popliteus" }
        ),
        DistortionSyndrome.FootSupination => (
            new[] { "Tibialis Posterior", "Tibialis Anterior", "Medial Gastrocnemius" },
            new[] { "Peroneals", "Intrinsic Foot Muscles" }
        ),
        DistortionSyndrome.SpinalAsymmetry => (
            new[] { "Paraspinals (Concave Side)", "Quadratus Lumborum (Elevated Side)" },
            new[] { "Paraspinals (Convex Side)", "Core Obliques", "Gluteus Medius" }
        ),
        _ => (Array.Empty<string>(), Array.Empty<string>())
    };

    /// <summary>
    /// Get the corrective exercise compensation strings that address a syndrome.
    /// These match the CorrectsCompensations field on Exercise entities.
    /// </summary>
    public static string[] GetTargetCompensations(DistortionSyndrome syndrome) => syndrome switch
    {
        DistortionSyndrome.UpperCrossed => new[]
            { "ForwardHead", "RoundedShoulders", "Kyphosis", "ArmsForward", "ShoulderElevation", "ScapularWinging" },
        DistortionSyndrome.LowerCrossed => new[]
            { "ExcessiveForwardLean", "LowBackArches", "AnteriorPelvicTilt", "HipDrop" },
        DistortionSyndrome.PronationDistortion => new[]
            { "FeetFlatten", "KneesValgus", "AnklePronation", "FeetTurnOut" },
        DistortionSyndrome.PosteriorPelvicTilt => new[]
            { "FlatBack", "PosteriorPelvicTilt", "LowBackRounds" },
        DistortionSyndrome.KneeHyperextension => new[]
            { "KneeHyperextension" },
        DistortionSyndrome.KneeVarus => new[]
            { "KneesVarus", "FeetTurnOut" },
        DistortionSyndrome.FootSupination => new[]
            { "FeetSupinated" },
        DistortionSyndrome.SpinalAsymmetry => new[]
            { "SpinalDeviation", "AsymmetricShift", "UnevenHips" },
        _ => Array.Empty<string>()
    };

    /// <summary>
    /// Get the daily life impact description for a syndrome.
    /// </summary>
    public static string GetDailyImpact(DistortionSyndrome syndrome) => syndrome switch
    {
        DistortionSyndrome.UpperCrossed =>
            "You may notice neck stiffness and headaches after computer work or driving. " +
            "Shoulder pain during overhead movements. Difficulty sleeping due to neck tension.",

        DistortionSyndrome.LowerCrossed =>
            "You may feel low back stiffness after sitting, tightness when standing up, " +
            "and hamstring strains during exercise. Difficulty standing for long periods.",

        DistortionSyndrome.PronationDistortion =>
            "You may experience arch pain at the end of the day, knee pain going downstairs, " +
            "shin splints during running, and shoes wearing unevenly on the inside edge.",

        DistortionSyndrome.PosteriorPelvicTilt =>
            "You may feel stiff getting out of a chair, have difficulty with hip flexion activities, " +
            "and notice a flat appearance in your low back.",

        DistortionSyndrome.KneeHyperextension =>
            "You may feel instability in your knees when standing, posterior knee pain, " +
            "and difficulty controlling knee position during squats.",

        DistortionSyndrome.KneeVarus =>
            "You may experience lateral knee pain, IT band tightness, " +
            "and difficulty keeping knees aligned during squats and lunges.",

        DistortionSyndrome.FootSupination =>
            "You may experience frequent ankle sprains, lateral foot pain, " +
            "and difficulty with balance on uneven surfaces.",

        DistortionSyndrome.SpinalAsymmetry =>
            "You may notice one-sided back pain, asymmetric gait, " +
            "and uneven shoulder or hip height when looking in the mirror.",

        _ => string.Empty
    };

    /// <summary>
    /// Score description based on movement quality score.
    /// </summary>
    public static string GetScoreDescription(int movementScore) => movementScore switch
    {
        >= 90 => "Excellent — minimal compensations detected. Focus on maintenance and performance.",
        >= 75 => "Good — some minor compensations. A short corrective warm-up will help.",
        >= 60 => "Moderate — several compensations present. Include corrective work in every session.",
        >= 40 => "Below Average — significant compensations. Prioritize corrective exercise before training.",
        _ => "Needs Attention — multiple significant dysfunctions. Focus primarily on corrective work."
    };
}
