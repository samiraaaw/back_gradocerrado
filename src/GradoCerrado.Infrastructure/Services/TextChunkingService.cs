// 📁 src/GradoCerrado.Infrastructure/Services/TextChunkingService.cs
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

namespace GradoCerrado.Infrastructure.Services;

/// <summary>
/// Servicio especializado para dividir texto en chunks optimizados para embeddings
/// </summary>
public interface ITextChunkingService
{
    /// <summary>
    /// Divide texto en chunks con tamaño y overlap configurables
    /// </summary>
    /// <param name="text">Texto a dividir</param>
    /// <param name="maxChunkSize">Tamaño máximo por chunk (en caracteres, ~4 chars = 1 token)</param>
    /// <param name="overlap">Solapamiento entre chunks para contexto</param>
    /// <returns>Lista de chunks de texto</returns>
    Task<List<string>> CreateChunksAsync(string text, int maxChunkSize = 500, int overlap = 100);

    /// <summary>
    /// Calcula el número aproximado de tokens para un texto
    /// </summary>
    int EstimateTokenCount(string text);
}

public class TextChunkingService : ITextChunkingService
{
    private readonly ILogger<TextChunkingService> _logger;

    // Configuración de chunking
    private const int DEFAULT_MAX_CHUNK_SIZE = 500;
    private const int DEFAULT_OVERLAP = 100;
    private const int CHARS_PER_TOKEN_ESTIMATE = 4; // Aproximación para español

    public TextChunkingService(ILogger<TextChunkingService> logger)
    {
        _logger = logger;
    }

    public async Task<List<string>> CreateChunksAsync(
        string text,
        int maxChunkSize = DEFAULT_MAX_CHUNK_SIZE,
        int overlap = DEFAULT_OVERLAP)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _logger.LogWarning("Intento de chunking con texto vacío");
            return new List<string>();
        }

        _logger.LogInformation("Iniciando chunking: {TotalChars} caracteres, maxSize={MaxSize}, overlap={Overlap}",
            text.Length, maxChunkSize, overlap);

        var chunks = new List<string>();

        // ESTRATEGIA 1: Dividir por párrafos primero
        var paragraphs = SplitIntoParagraphs(text);
        var currentChunk = new StringBuilder();

        foreach (var paragraph in paragraphs)
        {
            var cleanParagraph = paragraph.Trim();

            if (string.IsNullOrWhiteSpace(cleanParagraph))
                continue;

            // Si agregar este párrafo excedería el límite
            if (currentChunk.Length + cleanParagraph.Length > maxChunkSize && currentChunk.Length > 0)
            {
                // Guardar chunk actual
                var chunkText = currentChunk.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(chunkText))
                {
                    chunks.Add(chunkText);
                }

                // Nuevo chunk con overlap
                var overlapText = GetLastWords(chunkText, overlap);
                currentChunk.Clear();
                if (!string.IsNullOrWhiteSpace(overlapText))
                {
                    currentChunk.Append(overlapText + " ");
                }
            }

            // ESTRATEGIA 2: Si el párrafo mismo es muy largo, dividirlo por oraciones
            if (cleanParagraph.Length > maxChunkSize)
            {
                var sentences = SplitIntoSentences(cleanParagraph);
                foreach (var sentence in sentences)
                {
                    if (currentChunk.Length + sentence.Length > maxChunkSize && currentChunk.Length > 0)
                    {
                        var chunkText = currentChunk.ToString().Trim();
                        if (!string.IsNullOrWhiteSpace(chunkText))
                        {
                            chunks.Add(chunkText);
                        }

                        var overlapText = GetLastWords(chunkText, overlap);
                        currentChunk.Clear();
                        if (!string.IsNullOrWhiteSpace(overlapText))
                        {
                            currentChunk.Append(overlapText + " ");
                        }
                    }

                    currentChunk.Append(sentence + " ");
                }
            }
            else
            {
                currentChunk.Append(cleanParagraph + " ");
            }
        }

        // Agregar último chunk si no está vacío
        if (currentChunk.Length > 0)
        {
            var finalChunk = currentChunk.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(finalChunk))
            {
                chunks.Add(finalChunk);
            }
        }

        // ESTRATEGIA 3: Validación final - asegurar que ningún chunk exceda el límite
        var validatedChunks = await ValidateAndFixChunksAsync(chunks, maxChunkSize, overlap);

        _logger.LogInformation(
            "Chunking completado: {ChunkCount} chunks creados. Tamaño promedio: {AvgSize} caracteres (~{AvgTokens} tokens)",
            validatedChunks.Count,
            validatedChunks.Any() ? validatedChunks.Average(c => c.Length) : 0,
            validatedChunks.Any() ? validatedChunks.Average(c => EstimateTokenCount(c)) : 0);

        // Log de advertencia si hay chunks grandes
        var largeChunks = validatedChunks.Where(c => c.Length > maxChunkSize * 0.8).ToList();
        if (largeChunks.Any())
        {
            _logger.LogWarning(
                "{LargeCount} chunks son grandes (>80% del límite). Máximo: {MaxSize} caracteres (~{MaxTokens} tokens)",
                largeChunks.Count,
                largeChunks.Max(c => c.Length),
                largeChunks.Max(c => EstimateTokenCount(c)));
        }

        return validatedChunks;
    }

    public int EstimateTokenCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // Aproximación: 1 token ≈ 4 caracteres para español
        return (int)Math.Ceiling(text.Length / (double)CHARS_PER_TOKEN_ESTIMATE);
    }

    // ═══════════════════════════════════════════════════════════
    // MÉTODOS PRIVADOS DE SPLITTING
    // ═══════════════════════════════════════════════════════════

    private List<string> SplitIntoParagraphs(string text)
    {
        // Dividir por dobles saltos de línea o saltos de línea simples
        var paragraphs = Regex.Split(text, @"\n\n|\r\n\r\n|\n|\r\n")
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .ToList();

        return paragraphs.Any() ? paragraphs : new List<string> { text };
    }

    private List<string> SplitIntoSentences(string text)
    {
        // Dividir por puntos, signos de exclamación, interrogación
        // Regex mejorado para evitar dividir en abreviaturas comunes
        var sentences = Regex.Split(text, @"(?<=[.!?])\s+(?=[A-ZÁÉÍÓÚÑ])")
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Where(s => s.Length > 10) // Filtrar fragmentos muy cortos
            .ToList();

        return sentences.Any() ? sentences : new List<string> { text };
    }

    private string GetLastWords(string text, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length <= maxLength)
            return text;

        // Tomar aproximadamente las últimas N palabras (no más del 25% del texto)
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var wordCount = Math.Min(20, words.Length / 4); // Máximo 20 palabras o 25% del total
        var lastWords = words.TakeLast(wordCount);

        return string.Join(" ", lastWords);
    }

    // ═══════════════════════════════════════════════════════════
    // VALIDACIÓN Y CORRECCIÓN DE CHUNKS
    // ═══════════════════════════════════════════════════════════

    private async Task<List<string>> ValidateAndFixChunksAsync(
        List<string> chunks,
        int maxChunkSize,
        int overlap)
    {
        var validatedChunks = new List<string>();

        foreach (var chunk in chunks)
        {
            if (chunk.Length <= maxChunkSize)
            {
                validatedChunks.Add(chunk);
            }
            else
            {
                // Si un chunk todavía es muy largo, forzar división
                _logger.LogWarning("Chunk excede límite ({Size} chars), forzando división", chunk.Length);
                var subChunks = ForceChunkSplit(chunk, maxChunkSize, overlap);
                validatedChunks.AddRange(subChunks);
            }
        }

        return validatedChunks;
    }

    private List<string> ForceChunkSplit(string text, int maxSize, int overlap)
    {
        var chunks = new List<string>();
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var currentChunk = new StringBuilder();

        foreach (var word in words)
        {
            // Si agregar esta palabra excede el límite
            if (currentChunk.Length + word.Length + 1 > maxSize && currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());

                // Crear overlap con las últimas palabras
                var chunkText = currentChunk.ToString();
                var overlapText = GetLastWords(chunkText, overlap);
                currentChunk.Clear();
                if (!string.IsNullOrWhiteSpace(overlapText))
                {
                    currentChunk.Append(overlapText + " ");
                }
            }

            currentChunk.Append(word + " ");
        }

        if (currentChunk.Length > 0)
        {
            chunks.Add(currentChunk.ToString().Trim());
        }

        return chunks;
    }
}