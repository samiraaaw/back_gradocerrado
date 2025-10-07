// 📁 src/GradoCerrado.Domain/Entities/LegalDocument.cs
namespace GradoCerrado.Domain.Entities;

public class LegalDocument
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public LegalDocumentType DocumentType { get; set; }
    public List<string> LegalAreas { get; set; } = new();
    public List<string> KeyConcepts { get; set; } = new();
    public List<string> Articles { get; set; } = new();
    public List<string> Cases { get; set; } = new();
    public DifficultyLevel Difficulty { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Source { get; set; } = string.Empty;
    public bool IsProcessed { get; set; }
    public long FileSize { get; set; }
    public string MimeType { get; set; } = string.Empty;
    public List<DocumentChunk> Chunks { get; set; } = new();

    // Propiedades calculadas
    public bool HasBeenVectorized => Chunks.Any();
    public int ChunkCount => Chunks.Count;
    public DateTime? LastVectorizedAt => Chunks.Any() ? Chunks.Max(c => c.CreatedAt) : null;

    // Relación con preguntas generadas
    public List<StudyQuestion> GeneratedQuestions { get; set; } = new();
}