using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;
using IntelligentPersonalHealthOptimization.Services.Interfaces;

namespace IntelligentPersonalHealthOptimization.Services.Implementation;

public class AssessmentService : IAssessmentService
{
    private readonly IDatabaseService _databaseService;

    public AssessmentService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<AssessmentSession> StartNewSessionAsync(int userId)
    {
        var session = new AssessmentSession
        {
            UserId = userId,
            AssessmentDate = DateTime.UtcNow,
            IsComplete = false,
            CreatedAt = DateTime.UtcNow
        };
        await _databaseService.InsertAsync(session);
        return session;
    }

    public async Task<AssessmentResult> SaveResultAsync(int sessionId, AssessmentType type,
        List<MovementCompensation> compensations, int score, string notes)
    {
        var result = new AssessmentResult
        {
            AssessmentSessionId = sessionId,
            AssessmentType = type,
            DetectedCompensations = string.Join(",", compensations.Select(c => c.ToString())),
            Score = score,
            Notes = notes,
            CreatedAt = DateTime.UtcNow
        };
        await _databaseService.InsertAsync(result);
        return result;
    }

    public async Task CompleteSessionAsync(int sessionId)
    {
        var db = await _databaseService.GetConnectionAsync();
        var session = await db.Table<AssessmentSession>()
            .FirstOrDefaultAsync(s => s.Id == sessionId);

        if (session == null) return;

        var results = await GetResultsForSessionAsync(sessionId);
        session.OverallMovementScore = CalculateOverallScore(results);
        session.IsComplete = true;
        await _databaseService.UpdateAsync(session);
    }

    public async Task<AssessmentSession?> GetLatestSessionAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<AssessmentSession>()
            .Where(s => s.UserId == userId && s.IsComplete)
            .OrderByDescending(s => s.AssessmentDate)
            .FirstOrDefaultAsync();
    }

    public async Task<List<AssessmentResult>> GetResultsForSessionAsync(int sessionId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<AssessmentResult>()
            .Where(r => r.AssessmentSessionId == sessionId)
            .ToListAsync();
    }

    public async Task<List<AssessmentSession>> GetAllSessionsAsync(int userId)
    {
        var db = await _databaseService.GetConnectionAsync();
        return await db.Table<AssessmentSession>()
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.AssessmentDate)
            .ToListAsync();
    }

    public int CalculateOverallScore(List<AssessmentResult> results)
    {
        if (results.Count == 0) return 0;

        double weightedSum = 0;
        double totalWeight = 0;

        foreach (var result in results)
        {
            double weight = result.AssessmentType == AssessmentType.OverheadSquat ? 2.0 : 1.0;
            weightedSum += result.Score * 20.0 * weight; // Scale 1-5 to 0-100
            totalWeight += weight;
        }

        return (int)Math.Round(weightedSum / totalWeight);
    }
}
