// src/GradoCerrado.Application/Interfaces/IDocumentProcessingService.cs
using GradoCerrado.Domain.Entities;

namespace GradoCerrado.Application.Interfaces;

public interface IDocumentProcessingService
{
    Task<LegalDocument> ProcessDocumentAsync(string content, string fileName, LegalDocumentType? suggestedType = null);
    Task<List<string>> ExtractKeyConcepts(string content);
    Task<List<string>> IdentifyLegalAreas(string content);
    Task<DifficultyLevel> AssessDifficulty(string content);
    Task<List<string>> ExtractArticleReferences(string content);
    Task<List<string>> ExtractCaseReferences(string content);
}