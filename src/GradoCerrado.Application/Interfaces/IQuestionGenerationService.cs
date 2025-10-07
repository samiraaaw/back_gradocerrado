// ═══════════════════════════════════════════════════════════════════
// ARCHIVO: src/GradoCerrado.Application/Interfaces/IQuestionGenerationService.cs
// REEMPLAZAR COMPLETAMENTE
// ═══════════════════════════════════════════════════════════════════

using GradoCerrado.Domain.Entities;

namespace GradoCerrado.Application.Interfaces;

public interface IQuestionGenerationService
{
    /// <summary>
    /// Genera preguntas de un documento
    /// </summary>
    Task<List<StudyQuestion>> GenerateQuestionsFromDocument(
        LegalDocument document,
        int count = 10);

    /// <summary>
    /// 🆕 Genera preguntas con DISTRIBUCIÓN DE NIVELES (básico, intermedio, avanzado)
    /// </summary>
    Task<List<StudyQuestion>> GenerateQuestionsWithMixedDifficulty(
        LegalDocument document,
        int totalQuestions);

    /// <summary>
    /// Genera preguntas aleatorias de áreas y dificultad específicas
    /// </summary>
    Task<List<StudyQuestion>> GenerateRandomQuestions(
        List<string> legalAreas,
        DifficultyLevel difficulty,
        int count = 5);

    /// <summary>
    /// Genera pregunta de seguimiento basada en respuesta anterior
    /// </summary>
    Task<StudyQuestion> GenerateFollowUpQuestion(
        StudyQuestion originalQuestion,
        bool wasCorrect);

    /// <summary>
    /// Genera y guarda preguntas
    /// </summary>
    Task<List<StudyQuestion>> GenerateAndSaveQuestionsAsync(
        string topic,
        List<string> legalAreas,
        DifficultyLevel difficulty,
        int count = 5);



}

