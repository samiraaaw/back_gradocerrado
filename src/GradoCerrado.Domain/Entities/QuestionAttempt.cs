// 📁 src/GradoCerrado.Domain/Entities/QuestionAttempt.cs
namespace GradoCerrado.Domain.Entities;

public class QuestionAttempt
{
    public Guid Id { get; set; }
    public Guid QuestionId { get; set; }
    public Guid UserId { get; set; }
    public Guid? SessionId { get; set; }
    public string UserAnswer { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public DateTime AnsweredAt { get; set; }
    public TimeSpan TimeSpent { get; set; }
    public bool ViewedExplanation { get; set; } = false;
    public string? AIGeneratedExplanation { get; set; }

    // Relaciones
    public StudyQuestion Question { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public UserStudySession? Session { get; set; }
}