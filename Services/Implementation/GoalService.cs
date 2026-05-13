using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class GoalService : IGoalService
{
    private readonly IDatabaseService _databaseService;

    public GoalService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<UserGoal> CreateGoalAsync(UserGoal goal)
    {
        goal.CreatedAt = DateTime.UtcNow;
        goal.Status = GoalStatus.Active;
        await _databaseService.InsertAsync(goal);
        return goal;
    }

    public async Task<List<UserGoal>> GetActiveGoalsAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<UserGoal>()
            .Where(g => g.UserId == userId && g.Status == GoalStatus.Active)
            .ToListAsync();
    }

    public async Task<List<UserGoal>> GetAllGoalsAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<UserGoal>()
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync();
    }

    public async Task<UserGoal?> GetGoalByIdAsync(int goalId)
    {
        return await _databaseService.GetByIdAsync<UserGoal>(goalId);
    }

    public async Task<UserGoal> UpdateGoalProgressAsync(int goalId, double currentValue)
    {
        var goal = await _databaseService.GetByIdAsync<UserGoal>(goalId);
        if (goal == null) throw new InvalidOperationException("Goal not found");

        goal.CurrentValue = currentValue;

        // Auto-complete if target reached
        if (goal.TargetValue.HasValue && goal.CurrentValue.HasValue)
        {
            var progress = GetGoalProgressPercentage(goal);
            if (progress >= 100)
            {
                goal.Status = GoalStatus.Completed;
                goal.CompletedAt = DateTime.UtcNow;
            }
        }

        await _databaseService.UpdateAsync(goal);
        await CheckAndUpdateMilestonesAsync(goalId);
        return goal;
    }

    public async Task CompleteGoalAsync(int goalId)
    {
        var goal = await _databaseService.GetByIdAsync<UserGoal>(goalId);
        if (goal == null) return;

        goal.Status = GoalStatus.Completed;
        goal.CompletedAt = DateTime.UtcNow;
        await _databaseService.UpdateAsync(goal);
    }

    public async Task PauseGoalAsync(int goalId)
    {
        var goal = await _databaseService.GetByIdAsync<UserGoal>(goalId);
        if (goal == null) return;

        goal.Status = GoalStatus.Paused;
        await _databaseService.UpdateAsync(goal);
    }

    public async Task AbandonGoalAsync(int goalId)
    {
        var goal = await _databaseService.GetByIdAsync<UserGoal>(goalId);
        if (goal == null) return;

        goal.Status = GoalStatus.Abandoned;
        await _databaseService.UpdateAsync(goal);
    }

    public async Task<GoalMilestone> AddMilestoneAsync(int goalId, string title, double targetValue, int orderIndex)
    {
        var milestone = new GoalMilestone
        {
            UserGoalId = goalId,
            Title = title,
            TargetValue = targetValue,
            OrderIndex = orderIndex
        };
        await _databaseService.InsertAsync(milestone);
        return milestone;
    }

    public async Task<List<GoalMilestone>> GetMilestonesAsync(int goalId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<GoalMilestone>()
            .Where(m => m.UserGoalId == goalId)
            .OrderBy(m => m.OrderIndex)
            .ToListAsync();
    }

    public async Task CheckAndUpdateMilestonesAsync(int goalId)
    {
        var goal = await _databaseService.GetByIdAsync<UserGoal>(goalId);
        if (goal?.CurrentValue == null) return;

        var milestones = await GetMilestonesAsync(goalId);
        foreach (var milestone in milestones)
        {
            if (milestone.IsReached) continue;

            // For weight loss: current should be <= milestone target
            // For gains: current should be >= milestone target
            bool reached = goal.GoalCategory == GoalCategory.WeightLoss
                ? goal.CurrentValue <= milestone.TargetValue
                : goal.CurrentValue >= milestone.TargetValue;

            if (reached)
            {
                milestone.IsReached = true;
                milestone.ReachedDate = DateTime.UtcNow;
                await _databaseService.UpdateAsync(milestone);
            }
        }
    }

    public double GetGoalProgressPercentage(UserGoal goal)
    {
        if (!goal.TargetValue.HasValue || !goal.StartValue.HasValue || !goal.CurrentValue.HasValue)
            return 0;

        var totalChange = Math.Abs(goal.TargetValue.Value - goal.StartValue.Value);
        if (totalChange == 0) return 100;

        var currentChange = Math.Abs(goal.CurrentValue.Value - goal.StartValue.Value);
        var percentage = currentChange / totalChange * 100;
        return Math.Min(Math.Max(percentage, 0), 100);
    }
}
