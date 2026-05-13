namespace IntelligentPersonalHealthOptimization.Models.Enums;

public enum PostureDeviation
{
    // Anterior (Front) View
    ForwardHead,
    RoundedShoulders,
    UnevenShoulders,
    UnevenHips,
    FeetPronated,
    FeetSupinated,
    FeetTurnedOut,
    KneesValgus,
    KneesVarus,

    // Lateral (Side) View
    Kyphosis,
    Lordosis,
    FlatBack,
    Swayback,
    AnteriorPelvicTilt,
    PosteriorPelvicTilt,
    KneesHyperextended,
    ForwardShoulders,

    // Posterior (Back) View
    ScapularWinging,
    SpinalDeviation,
    CalcanealEversion,

    // Push/Pull Assessment
    ShoulderHiking,
    HeadProtrusion,
    LowBackSag,
    LowBackExtension
}
