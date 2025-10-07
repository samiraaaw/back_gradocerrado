// 📁 src/GradoCerrado.Infrastructure/DTOs/QuestionResponseDTOs.cs
// ✅ VERSIÓN FUSIONADA: DTOs existentes + nueva funcionalidad
using GradoCerrado.Domain.Entities;
using System.Text.Json.Serialization;

namespace GradoCerrado.Infrastructure.DTOs;

// ═══════════════════════════════════════════════════════════
// INTERFAZ BASE (NUEVO)
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Interfaz común para todas las respuestas de IA que contienen preguntas
/// </summary>
public interface IQuestionResponse
{
    /// <summary>
    /// Convierte la respuesta de IA a entidades StudyQuestion del dominio
    /// </summary>
    List<StudyQuestion> ToStudyQuestions(string legalArea, DifficultyLevel difficulty, Guid? documentId = null);
}

// ═══════════════════════════════════════════════════════════
// RESPUESTAS DE SELECCIÓN MÚLTIPLE (ACTUALIZADO)
// ═══════════════════════════════════════════════════════════

public class MultipleChoiceResponse : IQuestionResponse
{
    [JsonPropertyName("questions")]
    public List<MultipleChoiceQuestionDto> Questions { get; set; } = new();

    // ✅ NUEVO: Método de conversión
    public List<StudyQuestion> ToStudyQuestions(string legalArea, DifficultyLevel difficulty, Guid? documentId = null)
    {
        return Questions.Select(q => new StudyQuestion
        {
            Id = Guid.NewGuid(),
            QuestionText = q.QuestionText,
            Type = QuestionType.MultipleChoice,
            Options = q.Options?.Select(o => new QuestionOption
            {
                Id = Guid.NewGuid(),
                Text = o.Text,
                IsCorrect = o.IsCorrect
            }).ToList() ?? new List<QuestionOption>(),
            CorrectAnswer = q.Options?.FirstOrDefault(o => o.IsCorrect)?.Id ?? "",
            Explanation = q.Explanation,
            LegalArea = legalArea,
            RelatedConcepts = q.RelatedConcepts ?? new List<string>(),
            Difficulty = difficulty,
            SourceDocumentIds = documentId.HasValue ? new List<Guid> { documentId.Value } : new List<Guid>(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        }).ToList();
    }
}

public class MultipleChoiceQuestionDto
{
    [JsonPropertyName("questionText")]
    public string QuestionText { get; set; } = "";

    [JsonPropertyName("options")]
    public List<QuestionOptionDto> Options { get; set; } = new();

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = "";

    [JsonPropertyName("relatedConcepts")]
    public List<string> RelatedConcepts { get; set; } = new();

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "";
}

// ═══════════════════════════════════════════════════════════
// RESPUESTAS DE VERDADERO/FALSO (ACTUALIZADO)
// ═══════════════════════════════════════════════════════════

public class TrueFalseResponse : IQuestionResponse
{
    [JsonPropertyName("questions")]
    public List<TrueFalseQuestionDto> Questions { get; set; } = new();

    // ✅ NUEVO: Método de conversión
    public List<StudyQuestion> ToStudyQuestions(string legalArea, DifficultyLevel difficulty, Guid? documentId = null)
    {
        return Questions.Select(q => new StudyQuestion
        {
            Id = Guid.NewGuid(),
            QuestionText = q.QuestionText,
            Type = QuestionType.TrueFalse,
            Options = new List<QuestionOption>
            {
                new() { Id = Guid.NewGuid(), Text = "Verdadero", IsCorrect = q.IsTrue },
                new() { Id = Guid.NewGuid(), Text = "Falso", IsCorrect = !q.IsTrue }
            },
            CorrectAnswer = q.IsTrue ? "True" : "False",
            IsTrue = q.IsTrue,
            Explanation = q.Explanation,
            LegalArea = legalArea,
            RelatedConcepts = q.RelatedConcepts ?? new List<string>(),
            Difficulty = difficulty,
            SourceDocumentIds = documentId.HasValue ? new List<Guid> { documentId.Value } : new List<Guid>(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        }).ToList();
    }
}

public class TrueFalseQuestionDto
{
    [JsonPropertyName("questionText")]
    public string QuestionText { get; set; } = "";

    [JsonPropertyName("isTrue")]
    public bool IsTrue { get; set; }

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = "";

    [JsonPropertyName("relatedConcepts")]
    public List<string> RelatedConcepts { get; set; } = new();

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = "";
}

// ═══════════════════════════════════════════════════════════
// DTOs DE OPCIONES (YA EXISTENTES - SIN CAMBIOS)
// ═══════════════════════════════════════════════════════════

public class QuestionOptionDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("text")]
    public string Text { get; set; } = "";

    [JsonPropertyName("isCorrect")]
    public bool IsCorrect { get; set; }
}

public class OptionDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    [JsonPropertyName("isCorrect")]
    public bool IsCorrect { get; set; }
}

// ═══════════════════════════════════════════════════════════
// RESPUESTA DE PREGUNTA ÚNICA (ACTUALIZADO)
// ═══════════════════════════════════════════════════════════

public class SingleQuestionResponse : IQuestionResponse
{
    [JsonPropertyName("questionText")]
    public string QuestionText { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("options")]
    public List<OptionDto>? Options { get; set; }

    [JsonPropertyName("isTrue")]
    public bool? IsTrue { get; set; }

    [JsonPropertyName("correctAnswer")]
    public string CorrectAnswer { get; set; } = string.Empty;

    [JsonPropertyName("explanation")]
    public string Explanation { get; set; } = string.Empty;

    [JsonPropertyName("relatedConcepts")]
    public List<string> RelatedConcepts { get; set; } = new();

    [JsonPropertyName("difficulty")]
    public string Difficulty { get; set; } = string.Empty;

    // ✅ NUEVO: Método de conversión
    public List<StudyQuestion> ToStudyQuestions(string legalArea, DifficultyLevel difficulty, Guid? documentId = null)
    {
        var questionType = Type.ToLower() switch
        {
            "truefalse" or "true_false" => QuestionType.TrueFalse,
            "multiplechoice" or "multiple_choice" => QuestionType.MultipleChoice,
            _ => QuestionType.MultipleChoice
        };

        var question = new StudyQuestion
        {
            Id = Guid.NewGuid(),
            QuestionText = QuestionText,
            Type = questionType,
            CorrectAnswer = CorrectAnswer,
            Explanation = Explanation,
            LegalArea = legalArea,
            RelatedConcepts = RelatedConcepts,
            Difficulty = difficulty,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        // Configurar opciones según el tipo
        if (questionType == QuestionType.TrueFalse)
        {
            question.Options = new List<QuestionOption>
            {
                new() { Id = Guid.NewGuid(), Text = "Verdadero", IsCorrect = IsTrue ?? false },
                new() { Id = Guid.NewGuid(), Text = "Falso", IsCorrect = !(IsTrue ?? false) }
            };
            question.IsTrue = IsTrue;
        }
        else if (Options != null && Options.Any())
        {
            question.Options = Options.Select(o => new QuestionOption
            {
                Id = Guid.NewGuid(),
                Text = o.Text,
                IsCorrect = o.IsCorrect
            }).ToList();
        }

        return new List<StudyQuestion> { question };
    }
}

// ═══════════════════════════════════════════════════════════
// HELPER PARA PARSING GENÉRICO
// ═══════════════════════════════════════════════════════════

/// <summary>
/// Ayudante estático para parsing de JSON de OpenAI
/// </summary>
public static class QuestionJsonParser
{
    /// <summary>
    /// Extrae JSON válido de una respuesta que puede contener texto adicional
    /// </summary>
    public static string ExtractJson(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return "{}";

        // Buscar el JSON en la respuesta
        var startIndex = response.IndexOf('{');
        var endIndex = response.LastIndexOf('}');

        if (startIndex >= 0 && endIndex > startIndex)
        {
            return response.Substring(startIndex, endIndex - startIndex + 1);
        }

        return response;
    }
}