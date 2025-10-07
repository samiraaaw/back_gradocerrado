// src/GradoCerrado.Application/Interfaces/ILegalDocumentService.cs
using GradoCerrado.Domain.Entities;

namespace GradoCerrado.Application.Interfaces;

/// <summary>
/// Servicio ORQUESTADOR - SIN dependencias de ASP.NET Core
/// ¿Por qué? Para mantener Clean Architecture
/// </summary>
public interface ILegalDocumentService
{
	/// <summary>
	/// FLUJO COMPLETO: Archivo → Extraer texto → Procesar → Chunking → Vectorización
	/// Usa FileUploadInfo en lugar de IFormFile (abstracción)
	/// </summary>
	Task<DocumentProcessingResult> ProcessAndStoreDocumentAsync(FileUploadInfo fileInfo, DocumentUploadMetadata? metadata = null);

	/// <summary>
	/// FLUJO EXISTENTE: Texto directo → Procesar → Chunking → Vectorización  
	/// Compatible con tu funcionalidad actual
	/// </summary>
	Task<DocumentProcessingResult> ProcessAndStoreTextAsync(string content, string title, DocumentUploadMetadata? metadata = null);

	/// <summary>
	/// Obtiene documentos con filtros
	/// </summary>
	Task<List<LegalDocument>> GetLegalDocumentsAsync(
		LegalDocumentType? documentType = null,
		string? legalArea = null,
		DifficultyLevel? difficulty = null);

	/// <summary>
	/// Busca documentos por contenido (búsqueda semántica)
	/// </summary>
	Task<List<DocumentSearchResult>> SearchLegalDocumentsAsync(string query, int limit = 10);

	/// <summary>
	/// DELEGA A TU IQuestionGenerationService existente
	/// </summary>
	Task<List<StudyQuestion>> GenerateQuestionsFromDocumentAsync(Guid documentId, int questionCount = 5);

	/// <summary>
	/// Obtiene un documento específico
	/// </summary>
	Task<LegalDocument?> GetLegalDocumentByIdAsync(Guid documentId);

	/// <summary>
	/// Elimina documento completo
	/// </summary>
	Task<bool> DeleteLegalDocumentAsync(Guid documentId);

	/// <summary>
	/// Validación de archivos - usa abstracción
	/// </summary>
	bool ValidateFile(FileUploadInfo fileInfo);
}

// 📁 ABSTRACCIÓN de archivo - independiente de web
/// <summary>
/// Información de archivo subido - abstrae IFormFile
/// ¿Por qué? Para no depender de ASP.NET Core en Application layer
/// </summary>
public class FileUploadInfo
{
	public string FileName { get; set; } = string.Empty;
	public string ContentType { get; set; } = string.Empty;
	public long Length { get; set; }
	public Stream Content { get; set; } = Stream.Null;

	// Factory method para crear desde IFormFile (se usa en Infrastructure)
	public static async Task<FileUploadInfo> FromIFormFileAsync(object formFile)
	{
		// Este método se implementará en Infrastructure, no aquí
		throw new NotImplementedException("Se implementa en Infrastructure layer");
	}
}

// 📋 DTOs limpios sin dependencias web

/// <summary>
/// Metadatos opcionales para documentos
/// </summary>
public class DocumentUploadMetadata
{
	public string? Title { get; set; }
	public LegalDocumentType? DocumentType { get; set; }
	public List<string>? LegalAreas { get; set; }
	public DifficultyLevel? Difficulty { get; set; }
	public string? Source { get; set; }
}

/// <summary>
/// Resultado del procesamiento completo
/// </summary>
public class DocumentProcessingResult
{
	public bool Success { get; set; }
	public string? ErrorMessage { get; set; }
	public Guid? DocumentId { get; set; }
	public string? Title { get; set; }
	public int ChunksCreated { get; set; }
	public int TotalCharacters { get; set; }
	public List<string> ChunkIds { get; set; } = new();
	public TimeSpan ProcessingTime { get; set; }

	// Información extraída por TU IDocumentProcessingService
	public List<string> ExtractedConcepts { get; set; } = new();
	public List<string> IdentifiedAreas { get; set; } = new();
	public DifficultyLevel AssessedDifficulty { get; set; }
}

/// <summary>
/// Resultado de búsqueda semántica
/// </summary>
public class DocumentSearchResult
{
	public Guid DocumentId { get; set; }
	public string Title { get; set; } = string.Empty;
	public string Content { get; set; } = string.Empty;
	public double Score { get; set; }
	public LegalDocumentType DocumentType { get; set; }
	public List<string> LegalAreas { get; set; } = new();
	public List<string> KeyConcepts { get; set; } = new();
	public string HighlightedContent { get; set; } = string.Empty;
}