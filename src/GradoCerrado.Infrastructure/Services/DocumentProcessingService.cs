using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LangChain.Providers.OpenAI;
using Microsoft.Extensions.Logging;
using GradoCerrado.Domain.Entities;
using GradoCerrado.Application.Interfaces;

namespace GradoCerrado.Infrastructure.Services
{
    public class DocumentProcessingService : IDocumentProcessingService
    {
        private readonly ILogger<DocumentProcessingService> _logger;
        private readonly string _openAiKey;

        public DocumentProcessingService(ILogger<DocumentProcessingService> logger, string openAiKey)
        {
            _logger = logger;
            _openAiKey = openAiKey;
        }

        public async Task<LegalDocument> ProcessDocumentAsync(string content, string fileName, LegalDocumentType? suggestedType = null)
        {
            var concepts = await ExtractKeyConcepts(content);
            var areas = await IdentifyLegalAreas(content);
            var difficulty = await AssessDifficulty(content);

            return new LegalDocument
            {
                Id = Guid.NewGuid(),
                Title = fileName,
                Content = content,
                DocumentType = suggestedType ?? LegalDocumentType.StudyMaterial,
                Difficulty = difficulty,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public async Task<List<string>> ExtractKeyConcepts(string content)
        {
            try
            {
                var model = new OpenAiChatModel(_openAiKey, "gpt-4o-mini");

                var prompt = $@"
Extrae 5-10 conceptos jurídicos clave.
Lista separada por comas, sin números.

Texto:
{content.Substring(0, Math.Min(2000, content.Length))}

Formato: concepto1, concepto2, concepto3
";

                string responseText = "";
                await foreach (var response in model.GenerateAsync(prompt))
                {
                    responseText = response.Messages.Last().Content.Trim();
                }

                return responseText
                    .Split(',')
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrWhiteSpace(c))
                    .Take(10)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extrayendo conceptos");
                return new List<string> { "Derecho", "Legal" };
            }
        }

        public async Task<List<string>> IdentifyLegalAreas(string content)
        {
            try
            {
                var model = new OpenAiChatModel(_openAiKey, "gpt-4o-mini");

                var prompt = $@"
Identifica áreas del Derecho chileno.
Categorías: Derecho Civil, Penal, Procesal, Constitucional, Comercial, Laboral.
Lista separada por comas (máximo 3).

Texto:
{content.Substring(0, Math.Min(2000, content.Length))}
";

                string responseText = "";
                await foreach (var response in model.GenerateAsync(prompt))
                {
                    responseText = response.Messages.Last().Content.Trim();
                }

                var areas = responseText
                    .Split(',')
                    .Select(a => a.Trim())
                    .Where(a => !string.IsNullOrWhiteSpace(a))
                    .Take(3)
                    .ToList();

                return areas.Any() ? areas : new List<string> { "Derecho General" };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error identificando áreas");
                return new List<string> { "Derecho General" };
            }
        }

        public async Task<DifficultyLevel> AssessDifficulty(string content)
        {
            try
            {
                var model = new OpenAiChatModel(_openAiKey, "gpt-4o-mini");

                var prompt = $@"
Evalúa dificultad de este texto legal.
Responde UNA palabra: Basic, Intermediate o Advanced

Texto:
{content.Substring(0, Math.Min(1000, content.Length))}
";

                string responseText = "";
                await foreach (var response in model.GenerateAsync(prompt))
                {
                    responseText = response.Messages.Last().Content.Trim().ToLower();
                }

                if (responseText.Contains("basic") || responseText.Contains("básico"))
                    return DifficultyLevel.Basic;
                if (responseText.Contains("advanced") || responseText.Contains("avanzado"))
                    return DifficultyLevel.Advanced;

                return DifficultyLevel.Intermediate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error evaluando dificultad");
                return DifficultyLevel.Intermediate;
            }
        }

        public async Task<List<string>> ExtractArticleReferences(string content)
        {
            try
            {
                var model = new OpenAiChatModel(_openAiKey, "gpt-4o-mini");

                var prompt = $@"
Extrae referencias a artículos legales (ej: Art. 123, Artículo 45).
Lista separada por comas.

Texto:
{content.Substring(0, Math.Min(2000, content.Length))}
";

                string responseText = "";
                await foreach (var response in model.GenerateAsync(prompt))
                {
                    responseText = response.Messages.Last().Content.Trim();
                }

                return responseText
                    .Split(',')
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extrayendo referencias de artículos");
                return new List<string>();
            }
        }

        public async Task<List<string>> ExtractCaseReferences(string content)
        {
            try
            {
                var model = new OpenAiChatModel(_openAiKey, "gpt-4o-mini");

                var prompt = $@"
Extrae referencias a casos judiciales o sentencias.
Lista separada por comas.

Texto:
{content.Substring(0, Math.Min(2000, content.Length))}
";

                string responseText = "";
                await foreach (var response in model.GenerateAsync(prompt))
                {
                    responseText = response.Messages.Last().Content.Trim();
                }

                return responseText
                    .Split(',')
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrWhiteSpace(r))
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error extrayendo referencias de casos");
                return new List<string>();
            }
        }
    }
}