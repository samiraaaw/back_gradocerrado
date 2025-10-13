using LangChain.Providers.OpenAI;
using GradoCerrado.Application.Interfaces;
using GradoCerrado.Domain.Entities;
using GradoCerrado.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace GradoCerrado.Infrastructure.Services;

public class LangChainQuestionService : IAIService
{
    private readonly OpenAiProvider _provider;
    private readonly OpenAISettings _settings;
    private readonly ILogger<LangChainQuestionService> _logger;
    private readonly IRateLimiter _rateLimiter;

    public LangChainQuestionService(
        IOptions<OpenAISettings> settings,
        ILogger<LangChainQuestionService> logger,
        IRateLimiter rateLimiter)
    {
        _settings = settings.Value;
        _logger = logger;
        _rateLimiter = rateLimiter;
        _provider = new OpenAiProvider(_settings.ApiKey);
    }

    // ═══════════════════════════════════════════════════════════════
    // 1️⃣ GenerateStructuredQuestionsAsync
    // ═══════════════════════════════════════════════════════════════

    public async Task<string> GenerateStructuredQuestionsAsync(
        string sourceText,
        string legalArea,
        QuestionType type,
        DifficultyLevel difficulty,
        int count)
    {
        try
        {
            _logger.LogInformation(
                "🤖 Generando {Count} preguntas {Type} nivel {Difficulty}",
                count, type, difficulty);

            // 🆕 ESPERAR SLOT DISPONIBLE (Rate Limiting)
            await _rateLimiter.WaitIfNeededAsync();

            var model = new OpenAiChatModel(_provider, _settings.Model);

            // 🆕 Instrucciones específicas por nivel
            string difficultyInstructions = GetDifficultyInstructions(difficulty);

            string formatExample = type == QuestionType.MultipleChoice
                ? GetMultipleChoiceFormat(difficulty)
                : GetTrueFalseFormat(difficulty);

            var prompt = BuildEnhancedQuestionPrompt(
                sourceText, legalArea, type, difficulty,
                difficultyInstructions, count, formatExample);

            string responseText = "";
            await foreach (var response in model.GenerateAsync(
                $"Eres un experto en generar preguntas de examen de Derecho chileno. " +
                $"SOLO respondes en formato JSON válido.\n\n{prompt}"))
            {
                responseText = response.Messages.Last().Content;
            }

            // 🆕 REGISTRAR ÉXITO
            _rateLimiter.RecordRequest();

            string jsonResponse = ExtractJson(responseText);
            _logger.LogInformation("✅ Preguntas generadas exitosamente");

            return jsonResponse;
        }
        catch (Exception ex)
        {
            // 🆕 REGISTRAR ERROR
            _rateLimiter.RecordError();
            _logger.LogError(ex, "❌ Error generando preguntas");
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 2️⃣ GenerateAnswerExplanationAsync
    // ═══════════════════════════════════════════════════════════════

    public async Task<string> GenerateAnswerExplanationAsync(
        string questionText,
        string chosenAnswer,
        string correctAnswer,
        bool wasCorrect)
    {
        try
        {
            await _rateLimiter.WaitIfNeededAsync();

            var model = new OpenAiChatModel(_provider, _settings.Model);
            
            var prompt = BuildExplanationPrompt(
                questionText, chosenAnswer, correctAnswer, wasCorrect);

            string responseText = "";
            await foreach (var response in model.GenerateAsync(
                $"Eres un tutor paciente de Derecho chileno.\n\n{prompt}"))
            {
                responseText = response.Messages.Last().Content;
            }

            _rateLimiter.RecordRequest();
            return responseText.Trim();
        }
        catch (Exception ex)
        {
            _rateLimiter.RecordError();
            _logger.LogError(ex, "Error generando explicación");
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 3️⃣ EvaluateOralAnswerAsync
    // ═══════════════════════════════════════════════════════════════

    public async Task<string> EvaluateOralAnswerAsync(
        string questionText,
        string expectedAnswer,
        string explanation,
        string studentAnswer)
    {
        try
        {
            await _rateLimiter.WaitIfNeededAsync();

            var model = new OpenAiChatModel(_provider, _settings.Model);
            var prompt = $@"
Eres un evaluador experto de exámenes de grado de Derecho chileno.

PREGUNTA: {questionText}
RESPUESTA ESPERADA: {expectedAnswer}
EXPLICACIÓN: {explanation}
RESPUESTA DEL ESTUDIANTE: {studentAnswer}

Evalúa si es correcta considerando que es oral (puede tener errores de transcripción).
Enfócate en CONCEPTOS CLAVE.

RESPONDE EN JSON:
{{
    ""isCorrect"": true/false,
    ""confidence"": 0.0-1.0,
    ""evaluation"": ""CORRECTA/PARCIAL/INCORRECTA"",
    ""feedback"": ""Explicación para el estudiante""
}}";

            string responseText = "";
            await foreach (var response in model.GenerateAsync(
                $"Eres un evaluador justo de Derecho.\n\n{prompt}"))
            {
                responseText = response.Messages.Last().Content;
            }

            _rateLimiter.RecordRequest();
            return responseText;
        }
        catch (Exception ex)
        {
            _rateLimiter.RecordError();
            _logger.LogError(ex, "Error evaluando respuesta oral");
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // 4️⃣ GenerateResponseAsync
    // ═══════════════════════════════════════════════════════════════

    public async Task<string> GenerateResponseAsync(string prompt)
    {
        try
        {
            await _rateLimiter.WaitIfNeededAsync();

            var model = new OpenAiChatModel(_provider, _settings.Model);

            string responseText = "";
            await foreach (var response in model.GenerateAsync(prompt))
            {
                responseText = response.Messages.Last().Content;
            }

            _rateLimiter.RecordRequest();
            return responseText.Trim();
        }
        catch (Exception ex)
        {
            _rateLimiter.RecordError();
            _logger.LogError(ex, "Error generando respuesta");
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // MÉTODOS AUXILIARES PRIVADOS
    // ═══════════════════════════════════════════════════════════════

    private string GetDifficultyInstructions(DifficultyLevel difficulty)
    {
        return difficulty switch
        {
            DifficultyLevel.Basic => @"
NIVEL BÁSICO:
- Conceptos fundamentales e introductorios
- Definiciones simples del Código Civil
- Vocabulario jurídico básico
- Ejemplos de la vida cotidiana
- Comprensión literal del texto
- Preguntas directas sin interpretación compleja
- Respuestas que se encuentran explícitamente en el texto",

            DifficultyLevel.Intermediate => @"
NIVEL INTERMEDIO:
- Aplicación de conceptos ya conocidos
- Relación entre diferentes artículos
- Casos prácticos simples
- Vocabulario técnico moderado
- Requiere cierta interpretación
- Comparación entre conceptos
- Identificar excepciones a reglas generales",

            DifficultyLevel.Advanced => @"
NIVEL AVANZADO:
- Análisis profundo y crítico
- Conceptos abstractos y complejos
- Integración de múltiples artículos
- Casos complejos con varias variables
- Terminología técnica especializada
- Interpretación jurídica avanzada
- Excepciones y situaciones especiales",

            _ => "Nivel intermedio (default)"
        };
    }

    private string BuildEnhancedQuestionPrompt(
        string sourceText,
        string legalArea,
        QuestionType type,
        DifficultyLevel difficulty,
        string difficultyInstructions,
        int count,
        string formatExample)
    {
        return $@"
Genera EXACTAMENTE {count} preguntas de tipo {type} para examen de Derecho chileno.

ÁREA LEGAL: {legalArea}
DIFICULTAD: {difficulty}

{difficultyInstructions}

TEXTO FUENTE:
{sourceText}

INSTRUCCIONES CRÍTICAS:
1. Las preguntas deben basarse ÚNICAMENTE en el texto proporcionado
2. Ajusta la complejidad al nivel {difficulty} especificado
3. Genera EXACTAMENTE 3 opciones (A, B, C) por pregunta
4. Solo una opción debe ser correcta
5. Las opciones incorrectas deben ser plausibles pero claramente erróneas
6. Cada pregunta debe tener una explicación clara
7. Distribuye las preguntas en diferentes partes del texto

FORMATO JSON (responde SOLO JSON válido):
{formatExample}

IMPORTANTE: No agregues texto antes o después del JSON.
";
    }

    private string BuildExplanationPrompt(
        string questionText,
        string chosenAnswer,
        string correctAnswer,
        bool wasCorrect)
    {
        return $@"
Pregunta: {questionText}
Respuesta del estudiante: {chosenAnswer}
Respuesta correcta: {correctAnswer}
Resultado: {(wasCorrect ? "CORRECTA" : "INCORRECTA")}

Explica brevemente:
1) Por qué {(wasCorrect ? "es correcta" : "no es correcta")}
2) Fundamento conceptual
3) Consejo para recordar
";
    }

    private string GetMultipleChoiceFormat(DifficultyLevel difficulty)
    {
        string exampleQuestion = difficulty switch
        {
            DifficultyLevel.Basic => "¿Qué son los bienes corporales según el Código Civil?",
            DifficultyLevel.Intermediate => "¿Cuál es la principal diferencia entre bienes muebles e inmuebles?",
            DifficultyLevel.Advanced => "¿Cómo se efectúa la tradición de derechos personales según el artículo 699?",
            _ => "¿Pregunta de ejemplo?"
        };

        return $@"{{
  ""questions"": [
    {{
      ""questionText"": ""{exampleQuestion}"",
      ""options"": [
        {{""id"": ""A"", ""text"": ""Opción plausible incorrecta"", ""isCorrect"": false}},
        {{""id"": ""B"", ""text"": ""Respuesta correcta basada en el texto"", ""isCorrect"": true}},
        {{""id"": ""C"", ""text"": ""Opción plausible incorrecta"", ""isCorrect"": false}}
      ],
      ""explanation"": ""Explicación clara basada en el texto fuente"",
      ""relatedConcepts"": [""concepto1"", ""concepto2""],
      ""difficulty"": ""{difficulty}""
    }}
  ]
}}";
    }

    private string GetTrueFalseFormat(DifficultyLevel difficulty)
    {
        string exampleQuestion = difficulty switch
        {
            DifficultyLevel.Basic => "Los bienes corporales son aquellos que tienen un ser real.",
            DifficultyLevel.Intermediate => "La tradición de bienes inmuebles requiere siempre escritura pública.",
            DifficultyLevel.Advanced => "La prescripción adquisitiva extraordinaria no requiere título ni buena fe.",
            _ => "Afirmación de ejemplo"
        };

        return $@"{{
  ""questions"": [
    {{
      ""questionText"": ""{exampleQuestion}"",
      ""isTrue"": true,
      ""explanation"": ""Explicación basada en el artículo correspondiente"",
      ""relatedConcepts"": [""concepto1"", ""concepto2""],
      ""difficulty"": ""{difficulty}""
    }}
  ]
}}";
    }

    private string ExtractJson(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return "{}";

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');

        return (start >= 0 && end > start)
            ? text.Substring(start, end - start + 1)
            : text;
    }
}