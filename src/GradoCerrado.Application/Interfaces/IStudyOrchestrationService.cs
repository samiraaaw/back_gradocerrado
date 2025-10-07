// src/GradoCerrado.Application/Interfaces/IStudyOrchestrationService.cs
using GradoCerrado.Domain.Entities; // ← ESTA LÍNEA FALTABA

namespace GradoCerrado.Application.Interfaces;

public interface IStudyOrchestrationService
{
    // Generación de preguntas usando TUS interfaces existentes
    Task<List<StudyQuestion>> GenerateQuestionsFromTopicAsync(
        string topic,
        List<string> legalAreas,
        DifficultyLevel difficulty,
        int count = 5);

    // Sesiones de estudio
    Task<UserStudySession> StartStudySessionAsync(Guid userId, List<string> legalAreas, DifficultyLevel difficulty);
    Task<UserStudySession> EndStudySessionAsync(Guid sessionId);
    Task<QuestionAttempt> SubmitAnswerAsync(Guid sessionId, Guid questionId, string userAnswer, TimeSpan timeSpent);

    // Recomendaciones inteligentes
    Task<List<StudyQuestion>> GetRecommendedQuestionsAsync(Guid userId, int count = 5);
    Task<List<StudyQuestion>> GetNextQuestionsForSessionAsync(Guid sessionId, int count = 1);

    // Estadísticas
    Task<UserStudyStats> GetUserStatsAsync(Guid userId);
    Task<QuestionStats> GetQuestionStatsAsync(Guid questionId);
}

// DTOs simples para el orquestador
public class UserStudyStats
{
    public Guid UserId { get; set; }
    public int TotalQuestionsAttempted { get; set; }
    public int CorrectAnswers { get; set; }
    public double OverallSuccessRate { get; set; }
    public Dictionary<DifficultyLevel, int> QuestionsByDifficulty { get; set; } = new();
    public Dictionary<string, double> PerformanceByArea { get; set; } = new();
    public List<UserStudySession> RecentSessions { get; set; } = new();
}

public class QuestionStats
{
    public Guid QuestionId { get; set; }
    public int TotalAttempts { get; set; }
    public int CorrectAttempts { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageTimeSpent { get; set; }

}