namespace IntelligentPersonalHealthOptimization.Models.Enums;

/// <summary>
/// Distortion syndromes identified through CES assessment.
/// </summary>
public enum DistortionSyndrome
{
    None,
    UpperCrossed,           // Forward head + rounded shoulders + thoracic kyphosis
    LowerCrossed,           // Anterior pelvic tilt + lumbar lordosis + hip flexor dominance
    PronationDistortion,    // Flat feet + knee valgus + internal tibial rotation
    PosteriorPelvicTilt,    // Flat back + hamstring dominance
    KneeHyperextension,    // Genu recurvatum
    KneeVarus,             // Bow-legged
    FootSupination,         // High arch / underpronation
    SpinalAsymmetry         // Functional scoliosis
}

/// <summary>
/// Severity level for each identified syndrome or compensation.
/// </summary>
public enum CompensationSeverity
{
    None = 0,       // Not present
    Mild = 1,       // Appears on 1-2 of 5 reps, or barely visible
    Moderate = 2,   // Appears on 3-4 of 5 reps
    Severe = 3      // Appears on every rep, pronounced
}

/// <summary>
/// CES Corrective Exercise Continuum phase.
/// Used to structure corrective programs in the proper order.
/// </summary>
public enum CorrectivePhase
{
    Inhibit,    // SMR — foam rolling, lacrosse ball, reduce tension in overactive muscles
    Lengthen,   // Stretching — static, neuromuscular, restore muscle length
    Activate,   // Isolation — strengthen underactive muscles, low load, controlled tempo
    Integrate   // Functional — retrain coordinated movement patterns
}

/// <summary>
/// Risk level from the CES assessment.
/// </summary>
public enum CesRiskLevel
{
    Low = 1,        // Minimal compensations, proceed with normal training
    Moderate = 2,   // Some compensations, include corrective warm-up
    High = 3,       // Significant dysfunction, prioritize corrective work
    Referral = 4    // Pain or severe dysfunction, recommend professional evaluation
}
