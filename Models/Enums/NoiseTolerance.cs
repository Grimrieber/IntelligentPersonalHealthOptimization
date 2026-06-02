namespace IntelligentPersonalHealthOptimization.Models.Enums;

public enum NoiseTolerance
{
    NotApplicable = 0,
    Quiet,      // apartment, sleeping family — no plyo, no dropping
    Moderate,   // some noise OK, no heavy drops
    Loud        // garage/detached — plyo and drops fine
}
