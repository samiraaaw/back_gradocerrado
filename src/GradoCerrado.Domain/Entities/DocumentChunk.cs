// 📁 src/GradoCerrado.Domain/Entities/DocumentChunk.cs
namespace GradoCerrado.Domain.Entities;

public class DocumentChunk
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string Content { get; set; } = string.Empty;
    public int ChunkIndex { get; set; }
    public int StartPosition { get; set; }
    public int EndPosition { get; set; }
    public string VectorId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    // Relación
    public LegalDocument? Document { get; set; }
}