// 📁 src/GradoCerrado.Domain/Entities/UserStudySession.cs
namespace GradoCerrado.Domain.Entities;

public class UserStudySession
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public List<string> SelectedLegalAreas { get; set; } = new();
    public DifficultyLevel Difficulty { get; set; }
    public List<QuestionAttempt> QuestionAttempts { get; set; } = new();

    // Propiedades calculadas
    public int TotalQuestions => QuestionAttempts.Count;
    public int CorrectAnswers => QuestionAttempts.Count(qa => qa.IsCorrect);
    public double SuccessRate => TotalQuestions > 0 ? (double)CorrectAnswers / TotalQuestions : 0;

    // Relación
    public Student Student { get; set; } = null!;
}