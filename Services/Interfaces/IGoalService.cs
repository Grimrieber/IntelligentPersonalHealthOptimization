using IntelligentPersonalHealthOptimization.Models;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IGoalService
{
    Task<UserGoal> CreateGoalAsync(UserGoal goal);
    Task<List<UserGoal>> GetActiveGoalsAsync(int userId);
    Task<List<UserGoal>> GetAllGoalsAsync(int userId);
    Task<UserGoal?> GetGoalByIdAsync(int goalId);
    Task<UserGoal> UpdateGoalProgressAsync(int goalId, double currentValue);
    Task CompleteGoalAsync(int goalId);
    Task PauseGoalAsync(int goalId);
    Task AbandonGoalAsync(int goalId);
    Task<GoalMilestone> AddMilestoneAsync(int goalId, string title, double targetValue, int orderIndex);
    Task<List<GoalMilestone>> GetMilestonesAsync(int goalId);
    Task CheckAndUpdateMilestonesAsync(int goalId);
    double GetGoalProgressPercentage(UserGoal goal);
}
