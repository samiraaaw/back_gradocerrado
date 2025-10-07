using GradoCerrado.Application.Interfaces;
using GradoCerrado.Domain.Entities;
using GradoCerrado.Infrastructure.Configuration;
using GradoCerrado.Infrastructure.DTOs;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GradoCerrado.Infrastructure.Services;

public class QuestionGenerationService : IQuestionGenerationService
{
    private readonly IVectorService _vectorService;
    private readonly OpenAISettings _settings;
    private readonly ILogger<QuestionGenerationService> _logger;
    private readonly IAIService _aiService;

    public QuestionGenerationService(
        IOptions<OpenAISettings> settings,
        IVectorService vectorService,
        ILogger<QuestionGenerationService> logger,
        IAIService aiService)
    {
        _settings = settings.Value;
        _vectorService = vectorService;
        _logger = logger;
        _aiService = aiService;
    }

    public async Task<List<StudyQuestion>> GenerateQuestionsFromDocument(
        LegalDocument document,
        int questionCount = 10)
    {
        try
        {
            _logger.LogInformation(
                "Generando {Count} preguntas del documento {DocId}",
                questionCount, document.Id);

            var content = document.Content.Length > 3000
                ? document.Content.Substring(0, 3000)
                : document.Content;

            var multipleChoiceCount = (int)(questionCount * 0.7);
            var trueFalseCount = questionCount - multipleChoiceCount;

            var mcQuestions = await GenerateMultipleChoiceQuestions(
                content, document, multipleChoiceCount);

            var tfQuestions = await GenerateTrueFalseQuestions(
                content, document, trueFalseCount);

            var allQuestions = mcQuestions.Concat(tfQuestions).ToList();

            _logger.LogInformation(
                "Generadas {Total} preguntas",
                allQuestions.Count);

            return allQuestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando preguntas del documento");
            throw;
        }
    }

    public async Task<List<StudyQuestion>> GenerateRandomQuestions(
        List<string> legalAreas,
        DifficultyLevel difficulty,
        int count = 10)
    {
        try
        {
            _logger.LogInformation(
                "Generando {Count} preguntas aleatorias",
                count);

            var searchQuery = string.Join(" ", legalAreas);

            var relevantDocs = await _vectorService.SearchSimilarAsync(searchQuery, limit: 3);

            if (!relevantDocs.Any())
            {
                _logger.LogWarning("No se encontraron documentos relevantes");
                return new List<StudyQuestion>();
            }

            var content = string.Join("\n\n", relevantDocs.Select(d => d.Content));

            var questionsJson = await _aiService.GenerateStructuredQuestionsAsync(
                sourceText: content,
                legalArea: legalAreas.FirstOrDefault() ?? "Derecho General",
                type: QuestionType.MultipleChoice,
                difficulty: difficulty,
                count: count);

            return ParseQuestionResponse<MultipleChoiceResponse>(
                questionsJson,
                legalAreas.FirstOrDefault() ?? "Derecho General",
                difficulty,
                Guid.NewGuid());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando preguntas aleatorias");
            return new List<StudyQuestion>();
        }
    }

    public async Task<StudyQuestion> GenerateFollowUpQuestion(
        StudyQuestion previousQuestion,
        bool wasCorrect)
    {
        try
        {
            var difficulty = wasCorrect
                ? IncreaseDifficulty(previousQuestion.Difficulty)
                : DecreaseDifficulty(previousQuestion.Difficulty);

            var relatedConcepts = string.Join(", ", previousQuestion.RelatedConcepts);

            var questionsJson = await _aiService.GenerateStructuredQuestionsAsync(
                sourceText: $"Conceptos: {relatedConcepts}\nPregunta anterior: {previousQuestion.QuestionText}",
                legalArea: previousQuestion.LegalArea,
                type: previousQuestion.Type,
                difficulty: difficulty,
                count: 1);

            var questions = ParseQuestionResponse<MultipleChoiceResponse>(
                questionsJson,
                previousQuestion.LegalArea,
                difficulty,
                previousQuestion.SourceDocumentIds.FirstOrDefault());

            return questions.FirstOrDefault() ?? previousQuestion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando pregunta de seguimiento");
            return previousQuestion;
        }
    }

    public async Task<List<StudyQuestion>> GenerateAndSaveQuestionsAsync(
        string documentContent,
        List<string> legalAreas,
        DifficultyLevel difficulty,
        int count = 10)
    {
        try
        {
            var questionsJson = await _aiService.GenerateStructuredQuestionsAsync(
                sourceText: documentContent,
                legalArea: legalAreas.FirstOrDefault() ?? "Derecho General",
                type: QuestionType.MultipleChoice,
                difficulty: difficulty,
                count: count);

            var questions = ParseQuestionResponse<MultipleChoiceResponse>(
                questionsJson,
                legalAreas.FirstOrDefault() ?? "Derecho General",
                difficulty,
                Guid.NewGuid());

            return questions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando y guardando preguntas");
            throw;
        }
    }

    /// <summary>
    /// Genera preguntas con TODOS los niveles de dificultad
    /// </summary>
    public async Task<List<StudyQuestion>> GenerateQuestionsWithMixedDifficulty(
        LegalDocument document,
        int totalQuestions)
    {
        try
        {
            _logger.LogInformation(
                "Generando {Total} preguntas con distribución de niveles para documento {DocId}",
                totalQuestions, document.Id);

            var content = document.Content.Length > 5000
                ? document.Content.Substring(0, 5000)
                : document.Content;

            // DISTRIBUCIÓN: 30% básico, 40% intermedio, 30% avanzado
            var basicCount = (int)Math.Ceiling(totalQuestions * 0.30);
            var intermediateCount = (int)Math.Ceiling(totalQuestions * 0.40);
            var advancedCount = totalQuestions - basicCount - intermediateCount;

            _logger.LogInformation(
                "Distribución: {Basic} básicas, {Inter} intermedias, {Adv} avanzadas",
                basicCount, intermediateCount, advancedCount);

            var allQuestions = new List<StudyQuestion>();

            // Generar BÁSICAS
            if (basicCount > 0)
            {
                var basicQuestions = await GenerateQuestionsForDifficulty(
                    content, document, basicCount, DifficultyLevel.Basic
                );
                allQuestions.AddRange(basicQuestions);
                _logger.LogInformation("{Count} preguntas básicas generadas", basicQuestions.Count);
            }

            // Generar INTERMEDIAS
            if (intermediateCount > 0)
            {
                var interQuestions = await GenerateQuestionsForDifficulty(
                    content, document, intermediateCount, DifficultyLevel.Intermediate
                );
                allQuestions.AddRange(interQuestions);
                _logger.LogInformation("{Count} preguntas intermedias generadas", interQuestions.Count);
            }

            // Generar AVANZADAS
            if (advancedCount > 0)
            {
                var advQuestions = await GenerateQuestionsForDifficulty(
                    content, document, advancedCount, DifficultyLevel.Advanced
                );
                allQuestions.AddRange(advQuestions);
                _logger.LogInformation("{Count} preguntas avanzadas generadas", advQuestions.Count);
            }

            _logger.LogInformation("Total generado: {Total} preguntas", allQuestions.Count);

            return allQuestions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando preguntas con niveles mixtos");
            throw;
        }
    }

    private async Task<List<StudyQuestion>> GenerateMultipleChoiceQuestions(
        string content,
        LegalDocument document,
        int count)
    {
        try
        {
            var questionsJson = await _aiService.GenerateStructuredQuestionsAsync(
                sourceText: content,
                legalArea: document.LegalAreas.FirstOrDefault() ?? "Derecho General",
                type: QuestionType.MultipleChoice,
                difficulty: document.Difficulty,
                count: count);

            return ParseQuestionResponse<MultipleChoiceResponse>(
                questionsJson,
                document.LegalAreas.FirstOrDefault() ?? "Derecho General",
                document.Difficulty,
                document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando preguntas múltiple choice");
            return new List<StudyQuestion>();
        }
    }

    private async Task<List<StudyQuestion>> GenerateTrueFalseQuestions(
        string content,
        LegalDocument document,
        int count)
    {
        try
        {
            var questionsJson = await _aiService.GenerateStructuredQuestionsAsync(
                sourceText: content,
                legalArea: document.LegalAreas.FirstOrDefault() ?? "Derecho General",
                type: QuestionType.TrueFalse,
                difficulty: document.Difficulty,
                count: count);

            return ParseQuestionResponse<TrueFalseResponse>(
                questionsJson,
                document.LegalAreas.FirstOrDefault() ?? "Derecho General",
                document.Difficulty,
                document.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando preguntas verdadero/falso");
            return new List<StudyQuestion>();
        }
    }

    /// <summary>
    /// Genera preguntas para un nivel específico
    /// </summary>
    private async Task<List<StudyQuestion>> GenerateQuestionsForDifficulty(
        string content,
        LegalDocument document,
        int count,
        DifficultyLevel difficulty)
    {
        if (count <= 0)
            return new List<StudyQuestion>();

        // Distribución 70/30
        var multipleChoiceCount = (int)Math.Ceiling(count * 0.7);
        var trueFalseCount = count - multipleChoiceCount;

        var questions = new List<StudyQuestion>();

        // Generar Selección Múltiple
        if (multipleChoiceCount > 0)
        {
            var mcJson = await _aiService.GenerateStructuredQuestionsAsync(
                sourceText: content,
                legalArea: document.LegalAreas.FirstOrDefault() ?? "Derecho General",
                type: QuestionType.MultipleChoice,
                difficulty: difficulty,
                count: multipleChoiceCount
            );

            var mcQuestions = ParseQuestionResponse<MultipleChoiceResponse>(
                mcJson,
                document.LegalAreas.FirstOrDefault() ?? "Derecho General",
                difficulty,
                document.Id
            );

            questions.AddRange(mcQuestions);
        }

        // Generar Verdadero/Falso
        if (trueFalseCount > 0)
        {
            var tfJson = await _aiService.GenerateStructuredQuestionsAsync(
                sourceText: content,
                legalArea: document.LegalAreas.FirstOrDefault() ?? "Derecho General",
                type: QuestionType.TrueFalse,
                difficulty: difficulty,
                count: trueFalseCount
            );

            var tfQuestions = ParseQuestionResponse<TrueFalseResponse>(
                tfJson,
                document.LegalAreas.FirstOrDefault() ?? "Derecho General",
                difficulty,
                document.Id
            );

            questions.AddRange(tfQuestions);
        }

        return questions;
    }

    private List<StudyQuestion> ParseQuestionResponse<T>(
        string jsonResponse,
        string legalArea,
        DifficultyLevel difficulty,
        Guid documentId)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            if (typeof(T) == typeof(MultipleChoiceResponse))
            {
                var response = JsonSerializer.Deserialize<MultipleChoiceResponse>(
                    jsonResponse, options);

                return response?.Questions?.Select(q => new StudyQuestion
                {
                    Id = Guid.NewGuid(),
                    SourceDocumentIds = new List<Guid> { documentId },
                    QuestionText = q.QuestionText,
                    Type = QuestionType.MultipleChoice,
                    Difficulty = difficulty,
                    LegalArea = legalArea,
                    Options = q.Options.Select(o => new QuestionOption
                    {
                        Id = Guid.NewGuid(),
                        Text = o.Text,
                        IsCorrect = o.IsCorrect
                    }).ToList(),
                    CorrectAnswer = q.Options.First(o => o.IsCorrect).Text,
                    Explanation = q.Explanation,
                    RelatedConcepts = q.RelatedConcepts ?? new List<string>(),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                }).ToList() ?? new List<StudyQuestion>();
            }
            else if (typeof(T) == typeof(TrueFalseResponse))
            {
                var response = JsonSerializer.Deserialize<TrueFalseResponse>(
                    jsonResponse, options);

                return response?.Questions?.Select(q => new StudyQuestion
                {
                    Id = Guid.NewGuid(),
                    SourceDocumentIds = new List<Guid> { documentId },
                    QuestionText = q.QuestionText,
                    Type = QuestionType.TrueFalse,
                    Difficulty = difficulty,
                    LegalArea = legalArea,
                    IsTrue = q.IsTrue,
                    CorrectAnswer = q.IsTrue ? "Verdadero" : "Falso",
                    Explanation = q.Explanation,
                    RelatedConcepts = q.RelatedConcepts ?? new List<string>(),
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                }).ToList() ?? new List<StudyQuestion>();
            }

            return new List<StudyQuestion>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parseando respuesta JSON");
            return new List<StudyQuestion>();
        }
    }

    private DifficultyLevel IncreaseDifficulty(DifficultyLevel current)
    {
        return current switch
        {
            DifficultyLevel.Basic => DifficultyLevel.Intermediate,
            DifficultyLevel.Intermediate => DifficultyLevel.Advanced,
            DifficultyLevel.Advanced => DifficultyLevel.Advanced,
            _ => current
        };
    }

    private DifficultyLevel DecreaseDifficulty(DifficultyLevel current)
    {
        return current switch
        {
            DifficultyLevel.Advanced => DifficultyLevel.Intermediate,
            DifficultyLevel.Intermediate => DifficultyLevel.Basic,
            DifficultyLevel.Basic => DifficultyLevel.Basic,
            _ => current
        };
    }
}