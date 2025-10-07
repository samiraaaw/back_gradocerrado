// 📁 src/GradoCerrado.Domain/Entities/StudyQuestion.cs
namespace GradoCerrado.Domain.Entities;

public class StudyQuestion
{
    public Guid Id { get; set; }
    public List<string> SourceChunkIds { get; set; } = new();
    public string QuestionText { get; set; } = string.Empty;
    public QuestionType Type { get; set; }
    public List<QuestionOption> Options { get; set; } = new();
    public string CorrectAnswer { get; set; } = string.Empty;
    
    // ✅ SOLO AGREGAR ESTA LÍNEA:
    public bool? IsTrue { get; set; }
    
    public string Explanation { get; set; } = string.Empty;
    public string LegalArea { get; set; } = string.Empty;
    public List<string> RelatedConcepts { get; set; } = new();
    public DifficultyLevel Difficulty { get; set; }
    public List<Guid> SourceDocumentIds { get; set; } = new();
    public string SourceContext { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    // Estadísticas
    public int TimesAnswered { get; set; } = 0;
    public int TimesCorrect { get; set; } = 0;
    public double SuccessRate => TimesAnswered > 0 ? (double)TimesCorrect / TimesAnswered : 0;

    // Relaciones
    public List<QuestionAttempt> QuestionAttempts { get; set; } = new();
}