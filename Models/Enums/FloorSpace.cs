namespace IntelligentPersonalHealthOptimization.Models.Enums;

public enum FloorSpace
{
    NotApplicable = 0,
    Tight,      // under 6x6 ft — no deadlift setup, no broad jumps
    Moderate,   // 6x6 to 10x10 ft — most movements OK
    Open        // 10x10+ ft — sled, prowler, broad jumps available
}
