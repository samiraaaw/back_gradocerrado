// src/GradoCerrado.Application/Interfaces/IStudyQuestionRepository.cs
using GradoCerrado.Domain.Entities; // ← ESTA LÍNEA FALTABA

namespace GradoCerrado.Application.Interfaces;

public interface IStudyQuestionRepository
{
    // CRUD Preguntas
    Task<StudyQuestion> CreateAsync(StudyQuestion question);
    Task<StudyQuestion?> GetByIdAsync(Guid id);
    Task<List<StudyQuestion>> GetByLegalAreaAsync(string legalArea, int limit = 10);
    Task<List<StudyQuestion>> GetByDifficultyAsync(DifficultyLevel difficulty, int limit = 10);
    Task<List<StudyQuestion>> GetBySourceDocumentAsync(Guid documentId, int limit = 10);
    Task<List<StudyQuestion>> GetRandomAsync(List<string> legalAreas, DifficultyLevel difficulty, int count);
    Task UpdateAsync(StudyQuestion question);
    Task DeleteAsync(Guid id);

    // CRUD Sesiones de estudio
    Task<UserStudySession> CreateSessionAsync(UserStudySession session);
    Task<UserStudySession?> GetSessionByIdAsync(Guid id);
    Task<List<UserStudySession>> GetUserSessionsAsync(Guid userId, int limit = 20);
    Task UpdateSessionAsync(UserStudySession session);

    // CRUD Intentos de respuesta
    Task<QuestionAttempt> CreateAttemptAsync(QuestionAttempt attempt);
    Task<List<QuestionAttempt>> GetUserAttemptsAsync(Guid userId, int limit = 50);
    Task<List<QuestionAttempt>> GetQuestionAttemptsAsync(Guid questionId);

    // Estadísticas
    Task<Dictionary<DifficultyLevel, int>> GetUserStatsByDifficultyAsync(Guid userId);
    Task<Dictionary<string, double>> GetUserStatsByAreaAsync(Guid userId);
}