// src/GradoCerrado.Infrastructure/Services/ContentClassifierService.cs

using GradoCerrado.Application.Interfaces;
using GradoCerrado.Infrastructure.DTOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using GradoCerrado.Domain.Models;

namespace GradoCerrado.Infrastructure.Services;

public class ContentClassifierService : IContentClassifierService
{
    private readonly GradocerradoContext _context;
    private readonly IAIService _aiService;
    private readonly ILogger<ContentClassifierService> _logger;

    public ContentClassifierService(
        GradocerradoContext context,
        IAIService aiService,
        ILogger<ContentClassifierService> logger)
    {
        _context = context;
        _aiService = aiService;
        _logger = logger;
    }

    public async Task<List<ContentClassification>> ClassifyContentAsync(string content, int areaId)
    {
        try
        {
            // 1️⃣ Obtener temas y subtemas disponibles
            var temasDisponibles = await GetAvailableTemasAsync(areaId);

            if (!temasDisponibles.Any())
            {
                _logger.LogWarning("No hay temas disponibles para área {AreaId}", areaId);
                return new List<ContentClassification>();
            }

            // 2️⃣ Crear prompt para clasificación
            var prompt = BuildClassificationPrompt(content, temasDisponibles);

            // 3️⃣ Llamar a IA para clasificar
            var aiResponse = await _aiService.GenerateResponseAsync(prompt);

            // 4️⃣ Parsear respuesta JSON
            var classifications = ParseClassificationResponse(aiResponse, temasDisponibles);

            _logger.LogInformation(
                "Contenido clasificado en {Count} categorías para área {AreaId}",
                classifications.Count, areaId);

            return classifications;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clasificando contenido");
            throw;
        }
    }

    public async Task<List<TemaConSubtemas>> GetAvailableTemasAsync(int areaId)
    {
        try
        {
            var temas = await _context.Temas
                .Where(t => t.AreaId == areaId && t.Activo == true)
                .Select(t => new TemaConSubtemas
                {
                    TemaId = t.Id,
                    TemaNombre = t.Nombre,
                    Subtemas = t.Subtemas
                        .Where(s => s.Activo == true)
                        .Select(s => new SubtemaInfo
                        {
                            SubtemaId = s.Id,
                            SubtemaNombre = s.Nombre
                        })
                        .ToList()
                })
                .ToListAsync();

            return temas;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo temas disponibles");
            throw;
        }
    }

    private string BuildClassificationPrompt(string content, List<TemaConSubtemas> temasDisponibles)
    {
        var temasJson = JsonSerializer.Serialize(temasDisponibles, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        return $@"Eres un experto en clasificación de contenido legal chileno.

INSTRUCCIONES:
- Analiza el siguiente contenido y clasifícalo en los TEMAS Y SUBTEMAS EXISTENTES que se proporcionan
- NO inventes temas nuevos, SOLO usa los que están en la lista
- Puedes asignar el contenido a MÚLTIPLES temas si es relevante
- Para cada tema, intenta identificar el subtema más específico si aplica
- Asigna un nivel de confianza (0.0 a 1.0) para cada clasificación

TEMAS Y SUBTEMAS DISPONIBLES:
{temasJson}

CONTENIDO A CLASIFICAR:
{content.Substring(0, Math.Min(content.Length, 2000))}...

RESPONDE ESTRICTAMENTE EN ESTE FORMATO JSON:
{{
  ""classifications"": [
    {{
      ""temaId"": 1,
      ""temaNombre"": ""Derecho Civil"",
      ""subtemaId"": 5,
      ""subtemaNombre"": ""Contratos"",
      ""confidence"": 0.95
    }},
    {{
      ""temaId"": 2,
      ""temaNombre"": ""Derecho Comercial"",
      ""subtemaId"": null,
      ""subtemaNombre"": null,
      ""confidence"": 0.75
    }}
  ]
}}

IMPORTANTE:
- Solo incluye temas con confidence >= 0.6
- Ordena por confidence (mayor a menor)
- Máximo 3 clasificaciones
- Si el subtema no aplica, usa null";
    }

    private List<ContentClassification> ParseClassificationResponse(
        string aiResponse,
        List<TemaConSubtemas> temasDisponibles)
    {
        try
        {
            // Limpiar respuesta (remover markdown si existe)
            var jsonContent = ExtractJsonFromResponse(aiResponse);

            var response = JsonSerializer.Deserialize<ClassificationResponse>(
                jsonContent,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (response?.Classifications == null || !response.Classifications.Any())
            {
                _logger.LogWarning("No se encontraron clasificaciones en la respuesta de IA");
                return GetDefaultClassification(temasDisponibles);
            }

            // Validar que los IDs existen en los temas disponibles
            var validClassifications = response.Classifications
                .Where(c => ValidateClassification(c, temasDisponibles))
                .Where(c => c.Confidence >= 0.6)
                .OrderByDescending(c => c.Confidence)
                .Take(3)
                .Select(c => new ContentClassification
                {
                    TemaId = c.TemaId,
                    TemaNombre = c.TemaNombre,
                    SubtemaId = c.SubtemaId,
                    SubtemaNombre = c.SubtemaNombre,
                    Confidence = c.Confidence
                })
                .ToList();

            if (!validClassifications.Any())
            {
                _logger.LogWarning("Ninguna clasificación válida encontrada, usando default");
                return GetDefaultClassification(temasDisponibles);
            }

            return validClassifications;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parseando respuesta de clasificación");
            return GetDefaultClassification(temasDisponibles);
        }
    }

    private string ExtractJsonFromResponse(string response)
    {
        // Si la respuesta está en markdown code block, extraerla
        if (response.Contains("```json"))
        {
            var startIndex = response.IndexOf("```json") + 7;
            var endIndex = response.LastIndexOf("```");
            if (endIndex > startIndex)
            {
                return response.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }
        else if (response.Contains("```"))
        {
            var startIndex = response.IndexOf("```") + 3;
            var endIndex = response.LastIndexOf("```");
            if (endIndex > startIndex)
            {
                return response.Substring(startIndex, endIndex - startIndex).Trim();
            }
        }

        return response;
    }

    private bool ValidateClassification(
        ClassificationDto classification,
        List<TemaConSubtemas> temasDisponibles)
    {
        var tema = temasDisponibles.FirstOrDefault(t => t.TemaId == classification.TemaId);

        if (tema == null)
        {
            _logger.LogWarning("Tema ID {TemaId} no encontrado en temas disponibles", classification.TemaId);
            return false;
        }

        if (classification.SubtemaId.HasValue)
        {
            var subtema = tema.Subtemas.FirstOrDefault(s => s.SubtemaId == classification.SubtemaId.Value);
            if (subtema == null)
            {
                _logger.LogWarning(
                    "Subtema ID {SubtemaId} no encontrado en tema {TemaId}",
                    classification.SubtemaId, classification.TemaId);
                return false;
            }
        }

        return true;
    }

    private List<ContentClassification> GetDefaultClassification(List<TemaConSubtemas> temasDisponibles)
    {
        // Retornar el primer tema disponible como fallback
        var primerTema = temasDisponibles.FirstOrDefault();

        if (primerTema == null)
        {
            return new List<ContentClassification>();
        }

        return new List<ContentClassification>
        {
            new ContentClassification
            {
                TemaId = primerTema.TemaId,
                TemaNombre = primerTema.TemaNombre,
                SubtemaId = null,
                SubtemaNombre = null,
                Confidence = 0.5
            }
        };
    }

    // DTOs internos para parsing de la respuesta de IA
    private class ClassificationResponse
    {
        public List<ClassificationDto> Classifications { get; set; } = new();
    }

    private class ClassificationDto
    {
        public int TemaId { get; set; }
        public string TemaNombre { get; set; } = string.Empty;
        public int? SubtemaId { get; set; }
        public string? SubtemaNombre { get; set; }
        public double Confidence { get; set; }
    }
}