// 📁 src/GradoCerrado.Infrastructure/Services/MetadataBuilderService.cs
using GradoCerrado.Domain.Entities;

namespace GradoCerrado.Infrastructure.Services;

/// <summary>
/// DTO para información de archivo (sin depender de ASP.NET Core)
/// </summary>
public class FileInfo
{
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string ContentType { get; set; } = string.Empty;
}

/// <summary>
/// Servicio especializado para construir metadatos de documentos
/// </summary>
public interface IMetadataBuilderService
{
    Dictionary<string, object> BuildFromFile(LegalDocument document, FileInfo fileInfo);
    Dictionary<string, object> BuildFromText(LegalDocument document, string fileName, int contentLength, string source = "Manual");
    Dictionary<string, object> BuildMinimal(LegalDocument document, string? fileName = null);
    Dictionary<string, object> AddChunkInfo(Dictionary<string, object> baseMetadata, int chunkIndex, int totalChunks, Guid documentId);
}

public class MetadataBuilderService : IMetadataBuilderService
{
    // ═══════════════════════════════════════════════════════════
    // CONSTRUCCIÓN DESDE ARCHIVO
    // ═══════════════════════════════════════════════════════════

    public Dictionary<string, object> BuildFromFile(LegalDocument document, FileInfo fileInfo)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (fileInfo == null)
            throw new ArgumentNullException(nameof(fileInfo));

        return new Dictionary<string, object>
        {
            // Información del documento
            ["document_id"] = document.Id.ToString(),
            ["title"] = document.Title,
            ["document_type"] = document.DocumentType.ToString(),

            // Áreas y conceptos legales
            ["legal_areas"] = string.Join(",", document.LegalAreas),
            ["key_concepts"] = string.Join(",", document.KeyConcepts),
            ["articles"] = string.Join(",", document.Articles),
            ["cases"] = string.Join(",", document.Cases),

            // Dificultad y clasificación
            ["difficulty"] = document.Difficulty.ToString(),

            // ✅ INFORMACIÓN DEL ARCHIVO (usando fileInfo)
            ["file_name"] = fileInfo.FileName,
            ["file_size"] = fileInfo.FileSize,
            ["mime_type"] = fileInfo.ContentType,

            // Metadata adicional
            ["source"] = document.Source,
            ["created_at"] = document.CreatedAt.ToString("O"),

            // Flags de procesamiento
            ["is_processed"] = document.IsProcessed,
            ["has_questions"] = document.GeneratedQuestions?.Any() == true
        };
    }

    // ═══════════════════════════════════════════════════════════
    // CONSTRUCCIÓN DESDE TEXTO
    // ═══════════════════════════════════════════════════════════

    public Dictionary<string, object> BuildFromText(
        LegalDocument document,
        string fileName,
        int contentLength,
        string source = "Manual")
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("El nombre del archivo no puede estar vacío", nameof(fileName));

        return new Dictionary<string, object>
        {
            // Información del documento
            ["document_id"] = document.Id.ToString(),
            ["title"] = document.Title,
            ["document_type"] = document.DocumentType.ToString(),

            // Áreas y conceptos legales
            ["legal_areas"] = string.Join(",", document.LegalAreas),
            ["key_concepts"] = string.Join(",", document.KeyConcepts),
            ["articles"] = string.Join(",", document.Articles),
            ["cases"] = string.Join(",", document.Cases),

            // Dificultad
            ["difficulty"] = document.Difficulty.ToString(),

            // Información del "archivo" virtual
            ["file_name"] = fileName,
            ["file_size"] = contentLength,
            ["mime_type"] = "text/plain",

            // Metadata adicional
            ["source"] = source,
            ["created_at"] = document.CreatedAt.ToString("O"),

            // Flags de procesamiento
            ["is_processed"] = document.IsProcessed,
            ["has_questions"] = document.GeneratedQuestions?.Any() == true
        };
    }

    // ═══════════════════════════════════════════════════════════
    // CONSTRUCCIÓN MÍNIMA (para testing)
    // ═══════════════════════════════════════════════════════════

    public Dictionary<string, object> BuildMinimal(
        LegalDocument document,
        string? fileName = null)
    {
        if (document == null)
            throw new ArgumentNullException(nameof(document));

        return new Dictionary<string, object>
        {
            ["document_id"] = document.Id.ToString(),
            ["title"] = document.Title,
            ["document_type"] = document.DocumentType.ToString(),
            ["legal_areas"] = string.Join(",", document.LegalAreas),
            ["difficulty"] = document.Difficulty.ToString(),
            ["file_name"] = fileName ?? "documento.txt",
            ["source"] = "Test",
            ["created_at"] = document.CreatedAt.ToString("O")
        };
    }

    // ═══════════════════════════════════════════════════════════
    // AGREGAR INFORMACIÓN DE CHUNK
    // ═══════════════════════════════════════════════════════════

    public Dictionary<string, object> AddChunkInfo(
        Dictionary<string, object> baseMetadata,
        int chunkIndex,
        int totalChunks,
        Guid documentId)
    {
        if (baseMetadata == null)
            throw new ArgumentNullException(nameof(baseMetadata));

        if (chunkIndex < 0)
            throw new ArgumentException("El índice del chunk no puede ser negativo", nameof(chunkIndex));

        if (totalChunks <= 0)
            throw new ArgumentException("El total de chunks debe ser mayor a 0", nameof(totalChunks));

        // Crear una copia para no mutar el original
        var chunkMetadata = new Dictionary<string, object>(baseMetadata)
        {
            ["chunk_index"] = chunkIndex,
            ["chunk_id"] = $"{documentId}_chunk_{chunkIndex}",
            ["total_chunks"] = totalChunks,
            ["is_first_chunk"] = chunkIndex == 0,
            ["is_last_chunk"] = chunkIndex == totalChunks - 1
        };

        return chunkMetadata;
    }
}

// ═══════════════════════════════════════════════════════════
// EXTENSION METHODS
// ═══════════════════════════════════════════════════════════

public static class MetadataBuilderExtensions
{
    /// <summary>
    /// Crea una lista de metadatos para todos los chunks de un documento
    /// </summary>
    public static List<Dictionary<string, object>> BuildChunkMetadataList(
        this IMetadataBuilderService builder,
        Dictionary<string, object> baseMetadata,
        int totalChunks,
        Guid documentId)
    {
        var metadataList = new List<Dictionary<string, object>>();

        for (int i = 0; i < totalChunks; i++)
        {
            var chunkMetadata = builder.AddChunkInfo(baseMetadata, i, totalChunks, documentId);
            metadataList.Add(chunkMetadata);
        }

        return metadataList;
    }

    /// <summary>
    /// Obtiene un valor de metadatos de forma segura con valor por defecto
    /// </summary>
    public static T GetMetadataValue<T>(
        this Dictionary<string, object> metadata,
        string key,
        T defaultValue = default)
    {
        if (metadata == null || !metadata.ContainsKey(key))
            return defaultValue!;

        try
        {
            var value = metadata[key];
            if (value is T typedValue)
                return typedValue;

            // Intentar conversión
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch
        {
            return defaultValue!;
        }
    }
}