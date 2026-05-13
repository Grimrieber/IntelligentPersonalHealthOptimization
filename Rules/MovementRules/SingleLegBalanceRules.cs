using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Rules.MovementRules;

public static class SingleLegBalanceRules
{
    public static string GetCompensationExplanation(MovementCompensation comp) => comp switch
    {
        MovementCompensation.HipDrop => "Contralateral hip drop indicates gluteus medius weakness - a primary stabilizer of the pelvis during single-leg activities like walking and running.",
        MovementCompensation.TrunkLateralLean => "Leaning the trunk to one side is a compensatory strategy for hip abductor weakness, shifting the center of gravity over the stance leg.",
        MovementCompensation.AnklePronation => "Excessive ankle pronation during single-leg stance suggests weak intrinsic foot muscles and posterior tibialis, often linked to hip weakness.",
        MovementCompensation.ExcessiveMovement => "Significant balance loss indicates proprioceptive deficits and general neuromuscular instability that requires stability training.",
        _ => OverheadSquatRules.GetCompensationExplanation(comp)
    };
}
