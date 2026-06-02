namespace IntelligentPersonalHealthOptimization.Models.Enums;

public enum CeilingHeight
{
    NotApplicable = 0,
    Low,        // under 8 ft — strips overhead press, jumps, swings
    Standard,   // 8-9 ft — most movements OK
    High        // 9 ft+ — all overhead movements available
}
