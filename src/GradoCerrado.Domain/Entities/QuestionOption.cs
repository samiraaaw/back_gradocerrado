// 📁 src/GradoCerrado.Domain/Entities/QuestionOption.cs
namespace GradoCerrado.Domain.Entities;

public class QuestionOption
{
    public Guid Id { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
    public Guid QuestionId { get; set; }

    // Relación
    public StudyQuestion Question { get; set; } = null!;
}