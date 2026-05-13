namespace IntelligentPersonalHealthOptimization.Models.Enums;

public enum AssessmentType
{
    OverheadSquat,
    SingleLegBalanceLeft,
    SingleLegBalanceRight,

    // CES Static Posture Assessment
    PostureAnterior,    // Front view
    PostureLateral,     // Side view
    PosturePosterior,   // Back view

    // CES Additional Dynamic Assessments
    PushAssessment,     // Wall push-up / push-up observation
    PullAssessment      // Standing row observation
}
