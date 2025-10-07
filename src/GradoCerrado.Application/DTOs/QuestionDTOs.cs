// src/GradoCerrado.Application/DTOs/QuestionDTOs.cs
using GradoCerrado.Domain.Entities;

namespace GradoCerrado.Application.DTOs;

public class GenerateQuestionsRequest
{
    public string Topic { get; set; } = string.Empty;
    public int Count { get; set; } = 5;
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Intermediate;
    public QuestionType Type { get; set; } = QuestionType.MultipleChoice;
    public List<string> LegalAreas { get; set; } = new();
    public Guid? UserId { get; set; }
    public List<Guid>? SourceDocumentIds { get; set; }
}

public class QuestionResponse
{
    public Guid Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public List<QuestionOptionDto> Options { get; set; } = new();
    public QuestionType Type { get; set; }
    public DifficultyLevel Difficulty { get; set; }
    public string LegalArea { get; set; } = string.Empty;
    public List<string> RelatedConcepts { get; set; } = new();
    public List<Guid> SourceDocumentIds { get; set; } = new();
    public string SourceContext { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public double SuccessRate { get; set; }
    public string Explanation { get; set; } = string.Empty;
}

public class QuestionOptionDto
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; } // Solo incluir en respuestas, no en requests
}

public class StartStudySessionRequest
{
    public Guid UserId { get; set; }
    public List<string> SelectedLegalAreas { get; set; } = new();
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Intermediate;
}

public class StudySessionResponse
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<string> SelectedLegalAreas { get; set; } = new();
    public DifficultyLevel Difficulty { get; set; }
    public int TotalQuestions { get; set; }
    public int CorrectAnswers { get; set; }
    public double SuccessRate { get; set; }
    public bool IsCompleted => EndTime.HasValue;
}

public class SubmitAnswerRequest
{
    public Guid SessionId { get; set; }
    public Guid QuestionId { get; set; }
    public string UserAnswer { get; set; } = string.Empty;
    public TimeSpan TimeSpent { get; set; }
}

public class AnswerResponse
{
    public Guid AttemptId { get; set; }
    public bool IsCorrect { get; set; }
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string? AIGeneratedExplanation { get; set; }
    public DateTime AnsweredAt { get; set; }
    public double SuccessRate { get; set; } // Tasa de éxito de esta pregunta
}

public class UserStatsResponse
{
    public Guid UserId { get; set; }
    public int TotalQuestionsAttempted { get; set; }
    public int CorrectAnswers { get; set; }
    public double OverallSuccessRate { get; set; }
    public Dictionary<DifficultyLevel, int> QuestionsByDifficulty { get; set; } = new();
    public Dictionary<string, double> PerformanceByArea { get; set; } = new();
    public List<StudySessionResponse> RecentSessions { get; set; } = new();
    public DateTime LastActivity { get; set; }
}

public class QuestionStatsResponse
{
    public Guid QuestionId { get; set; }
    public int TotalAttempts { get; set; }
    public int CorrectAttempts { get; set; }
    public double SuccessRate { get; set; }
    public TimeSpan AverageTimeSpent { get; set; }
    public DifficultyLevel Difficulty { get; set; }
    public string LegalArea { get; set; } = string.Empty;
}

// DTOs para documentos legales
public class UploadDocumentRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public LegalDocumentType DocumentType { get; set; }
    public List<string> LegalAreas { get; set; } = new();
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Intermediate;
    public string Source { get; set; } = "Manual";
}

public class DocumentResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public LegalDocumentType DocumentType { get; set; }
    public List<string> LegalAreas { get; set; } = new();
    public List<string> KeyConcepts { get; set; } = new();
    public DifficultyLevel Difficulty { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsProcessed { get; set; }
    public bool HasBeenVectorized { get; set; }
    public int ChunkCount { get; set; }
    public DateTime? LastVectorizedAt { get; set; }
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
}

public class ProcessDocumentRequest
{
    public Guid DocumentId { get; set; }
    public bool GenerateQuestions { get; set; } = true;
    public int QuestionCount { get; set; } = 10;
    public DifficultyLevel QuestionDifficulty { get; set; } = DifficultyLevel.Intermediate;
}