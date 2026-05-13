using IntelligentPersonalHealthOptimization.Models;
using IntelligentPersonalHealthOptimization.Models.Enums;

namespace IntelligentPersonalHealthOptimization.Services.Interfaces;

public interface IAssessmentService
{
    Task<AssessmentSession> StartNewSessionAsync(int userId);
    Task<AssessmentResult> SaveResultAsync(int sessionId, AssessmentType type,
        List<MovementCompensation> compensations, int score, string notes);
    Task CompleteSessionAsync(int sessionId);
    Task<AssessmentSession?> GetLatestSessionAsync(int userId);
    Task<List<AssessmentResult>> GetResultsForSessionAsync(int sessionId);
    Task<List<AssessmentSession>> GetAllSessionsAsync(int userId);
    int CalculateOverallScore(List<AssessmentResult> results);
}
