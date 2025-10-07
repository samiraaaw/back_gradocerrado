// ═══════════════════════════════════════════════════════════════════
// ARCHIVO: src/GradoCerrado.Application/Interfaces/IAIService.cs
// REEMPLAZAR COMPLETAMENTE CON ESTO:
// ═══════════════════════════════════════════════════════════════════

using GradoCerrado.Domain.Entities;

namespace GradoCerrado.Application.Interfaces;

public interface IAIService
{
    /// <summary>
    /// Genera preguntas estructuradas en formato JSON
    /// </summary>
    Task<string> GenerateStructuredQuestionsAsync(
        string sourceText,
        string legalArea,
        QuestionType type,
        DifficultyLevel difficulty,
        int count);

    /// <summary>
    /// Genera explicación de una respuesta
    /// </summary>
    Task<string> GenerateAnswerExplanationAsync(
        string questionText,
        string chosenAnswer,
        string correctAnswer,
        bool wasCorrect);

    /// <summary>
    /// Evalúa una respuesta oral del estudiante
    /// </summary>
    Task<string> EvaluateOralAnswerAsync(
        string questionText,
        string expectedAnswer,
        string explanation,
        string studentAnswer);

    /// <summary>
    /// Genera una respuesta genérica para cualquier prompt
    /// </summary>
    Task<string> GenerateResponseAsync(string prompt);
}