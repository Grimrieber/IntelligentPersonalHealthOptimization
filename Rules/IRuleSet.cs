namespace IntelligentPersonalHealthOptimization.Rules;

public interface IRuleSet
{
    void Evaluate(RuleContext context, RuleResult result);
}
