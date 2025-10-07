using Microsoft.AspNetCore.Mvc;
using GradoCerrado.Application.Interfaces;
using GradoCerrado.Application.DTOs;
using GradoCerrado.Domain.Models;

namespace GradoCerrado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SpeechController : ControllerBase
{
    private readonly ISpeechService _speechService;
    private readonly ILogger<SpeechController> _logger;
    private readonly ITestRepository _testRepository;
    private readonly IPreguntaRepository _preguntaRepository;
    private readonly IAIService _aiService; // 🆕 AGREGAR SERVICIO DE IA

    public SpeechController(
        ISpeechService speechService,
        ILogger<SpeechController> logger,
        ITestRepository testRepository,
        IPreguntaRepository preguntaRepository,
        IAIService aiService) // 🆕 INYECTAR SERVICIO
    {
        _speechService = speechService;
        _logger = logger;
        _testRepository = testRepository;
        _preguntaRepository = preguntaRepository;
        _aiService = aiService; // 🆕 ASIGNAR SERVICIO
    }

    // ═══════════════════════════════════════════════════════════
    // SPEECH-TO-TEXT: Transcribe audio y guarda respuesta
    // ═══════════════════════════════════════════════════════════
    [HttpPost("speech-to-text")]
    public async Task<ActionResult> SpeechToText(
        IFormFile audioFile,
        [FromForm] int? testId = null,
        [FromForm] int? preguntaGeneradaId = null,
        [FromForm] short? numeroOrden = null,
        [FromForm] int? tiempoRespuestaSegundos = null)
    {
        try
        {
            _logger.LogInformation(
                "📝 Recibiendo audio - testId: {TestId}, preguntaId: {PreguntaId}, orden: {Orden}, tiempo: {Tiempo}s",
                testId, preguntaGeneradaId, numeroOrden, tiempoRespuestaSegundos);

            if (audioFile == null || audioFile.Length == 0)
            {
                return BadRequest(new { success = false, message = "Debe proporcionar un archivo de audio" });
            }

            var allowedTypes = new[] { ".wav", ".mp3", ".m4a", ".ogg", ".webm" };
            var fileExtension = Path.GetExtension(audioFile.FileName).ToLower();

            if (!allowedTypes.Contains(fileExtension))
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Tipo de archivo no soportado. Use: {string.Join(", ", allowedTypes)}"
                });
            }

            // 1️⃣ TRANSCRIBIR AUDIO
            using var stream = audioFile.OpenReadStream();
            var audioData = new byte[audioFile.Length];
            await stream.ReadAsync(audioData, 0, audioData.Length);

            var transcription = await _speechService.SpeechToTextAsync(audioData);

            if (string.IsNullOrWhiteSpace(transcription))
            {
                _logger.LogWarning("⚠️ Transcripción vacía");
                return Ok(new
                {
                    transcription = "",
                    success = false,
                    message = "No se pudo transcribir el audio. Intente grabar nuevamente."
                });
            }

            // 2️⃣ GUARDAR EN BD
            if (testId.HasValue && preguntaGeneradaId.HasValue && numeroOrden.HasValue)
            {
                try
                {
                    await SaveOrUpdateAnswerAsync(
                        testId.Value,
                        preguntaGeneradaId.Value,
                        numeroOrden.Value,
                        transcription,
                        tiempoRespuestaSegundos);

                    _logger.LogInformation("✅ Respuesta guardada exitosamente");
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "❌ Error guardando respuesta");
                }
            }

            // 3️⃣ RETORNAR TRANSCRIPCIÓN
            return Ok(new
            {
                transcription,
                success = true,
                audioSize = audioFile.Length,
                fileName = audioFile.FileName,
                format = fileExtension
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en speech-to-text");
            return StatusCode(500, new { success = false, error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 🆕 EVALUATE-ORAL-ANSWER: Evaluación inteligente con IA
    // ═══════════════════════════════════════════════════════════
    [HttpPost("evaluate-oral-answer")]
    public async Task<ActionResult> EvaluateOralAnswer([FromBody] EvaluateOralAnswerRequest request)
    {
        try
        {
            _logger.LogInformation(
                "🎤 Evaluando respuesta ORAL - testId: {TestId}, preguntaId: {PreguntaId}",
                request.TestId, request.PreguntaGeneradaId);

            // 1️⃣ OBTENER LA PREGUNTA
            var pregunta = await _preguntaRepository.GetByIdAsync(request.PreguntaGeneradaId);

            if (pregunta == null)
            {
                _logger.LogWarning("Pregunta no encontrada: {PreguntaId}", request.PreguntaGeneradaId);
                return NotFound(new { success = false, message = "Pregunta no encontrada" });
            }

            // 2️⃣ DETERMINAR MODALIDAD Y EVALUAR
            EvaluationResult evaluation;

            // 🆕 VERIFICAR SI ES MODALIDAD ORAL (modalidad_id = 2)
            bool isOralMode = pregunta.ModalidadId == 2;

            if (isOralMode)
            {
                // ✨ MODALIDAD ORAL: Usar IA para evaluación flexible
                _logger.LogInformation("📢 Modo ORAL detectado - usando evaluación por IA");
                evaluation = await EvaluateWithAIAsync(request.Transcription, pregunta);
            }
            else
            {
                // 📝 MODALIDAD ESCRITA: Usar evaluación tradicional
                _logger.LogInformation("✍️ Modo ESCRITO detectado - usando evaluación tradicional");

                if (pregunta.Tipo == "seleccion_multiple")
                {
                    evaluation = await EvaluateMultipleChoiceAsync(
                        request.Transcription,
                        pregunta,
                        request.PreguntaGeneradaId);
                }
                else if (pregunta.Tipo == "verdadero_falso")
                {
                    evaluation = EvaluateTrueFalse(request.Transcription, pregunta);
                }
                else
                {
                    _logger.LogWarning("Tipo de pregunta no soportado: {Tipo}", pregunta.Tipo);
                    return BadRequest(new { success = false, message = "Tipo de pregunta no soportado" });
                }
            }

            // 3️⃣ GUARDAR EVALUACIÓN EN BASE DE DATOS
            var testPregunta = await _testRepository.GetTestPreguntaAsync(
                request.TestId,
                request.NumeroOrden);

            if (testPregunta != null)
            {
                testPregunta.EsCorrecta = evaluation.IsCorrect;
                await _testRepository.UpdateTestPreguntaAsync(testPregunta);

                _logger.LogInformation(
                    "✅ Evaluación guardada - Correcta: {IsCorrect}, Confianza: {Confidence}%, Modo: {Mode}",
                    evaluation.IsCorrect,
                    (int)(evaluation.Confidence * 100),
                    isOralMode ? "ORAL" : "ESCRITO");
            }

            // 4️⃣ RETORNAR RESULTADO
            return Ok(new
            {
                success = true,
                isCorrect = evaluation.IsCorrect,
                confidence = (int)(evaluation.Confidence * 100),
                correctAnswer = evaluation.CorrectAnswerText,
                explanation = evaluation.Explanation ?? pregunta.Explicacion ?? "No hay explicación disponible.",
                questionText = pregunta.TextoPregunta,
                evaluationMode = isOralMode ? "AI" : "Traditional",
                feedback = GenerateFeedback(evaluation.IsCorrect, evaluation.Confidence, pregunta, isOralMode)
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error evaluando respuesta oral");
            return StatusCode(500, new
            {
                success = false,
                message = "Error al evaluar respuesta",
                error = ex.Message
            });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 🆕 EVALUACIÓN CON IA (para preguntas orales abiertas)
    // ═══════════════════════════════════════════════════════════
    private async Task<EvaluationResult> EvaluateWithAIAsync(
        string transcription,
        PreguntasGenerada pregunta)
    {
        try
        {
            _logger.LogInformation("🤖 Consultando IA para evaluar respuesta oral");

            // Construir prompt para la IA
            string evaluationPrompt = $@"
Eres un evaluador experto de exámenes de grado de Derecho chileno.

PREGUNTA:
{pregunta.TextoPregunta}

RESPUESTA CORRECTA ESPERADA:
{pregunta.RespuestaModelo ?? "Ver explicación"}

EXPLICACIÓN OFICIAL:
{pregunta.Explicacion ?? "No disponible"}

RESPUESTA DEL ESTUDIANTE (transcrita de audio):
{transcription}

INSTRUCCIONES:
1. Evalúa si la respuesta del estudiante es CORRECTA, PARCIALMENTE CORRECTA o INCORRECTA
2. Considera que es una respuesta oral, puede tener:
   - Reformulaciones naturales del lenguaje hablado
   - Pequeños errores de transcripción
   - Estructura menos formal que una respuesta escrita
3. Enfócate en los CONCEPTOS CLAVE y la COMPRENSIÓN DEL TEMA
4. Sé flexible con la forma pero estricto con el fondo

RESPONDE EN JSON con este formato:
{{
    ""isCorrect"": true/false,
    ""confidence"": 0.0-1.0,
    ""evaluation"": ""CORRECTA/PARCIAL/INCORRECTA"",
    ""keyPointsCovered"": [""punto1"", ""punto2""],
    ""keyPointsMissing"": [""punto1"", ""punto2""],
    ""feedback"": ""Explicación detallada para el estudiante""
}}";

            // Consultar a la IA
            var aiResponse = await _aiService.GenerateResponseAsync(evaluationPrompt);

            // Parsear respuesta JSON
            var evaluation = ParseAIEvaluation(aiResponse, pregunta);

            _logger.LogInformation(
                "🎯 IA evaluó como: {Result} (confianza: {Confidence}%)",
                evaluation.IsCorrect ? "CORRECTA" : "INCORRECTA",
                (int)(evaluation.Confidence * 100));

            return evaluation;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en evaluación con IA");

            // Fallback: evaluación conservadora
            return new EvaluationResult
            {
                IsCorrect = false,
                Confidence = 0.3,
                CorrectAnswerText = pregunta.RespuestaModelo ?? "Ver explicación de la pregunta",
                Explanation = $"No se pudo evaluar automáticamente. Revisa tu respuesta con el material de estudio. {pregunta.Explicacion}"
            };
        }
    }

    // ═══════════════════════════════════════════════════════════
    // PARSER DE RESPUESTA DE IA
    // ═══════════════════════════════════════════════════════════
    private EvaluationResult ParseAIEvaluation(string aiResponse, PreguntasGenerada pregunta)
    {
        try
        {
            // Extraer JSON de la respuesta
            var jsonStart = aiResponse.IndexOf('{');
            var jsonEnd = aiResponse.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonText = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var evaluation = System.Text.Json.JsonSerializer.Deserialize<AIEvaluationResponse>(
                    jsonText,
                    new System.Text.Json.JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (evaluation != null)
                {
                    return new EvaluationResult
                    {
                        IsCorrect = evaluation.IsCorrect,
                        Confidence = evaluation.Confidence,
                        CorrectAnswerText = pregunta.RespuestaModelo ?? "Ver explicación",
                        Explanation = evaluation.Feedback
                    };
                }
            }

            // Si no se puede parsear, intentar análisis simple del texto
            bool containsCorrect = aiResponse.ToLower().Contains("correcta") ||
                                  aiResponse.ToLower().Contains("correcto");
            bool containsIncorrect = aiResponse.ToLower().Contains("incorrecta") ||
                                    aiResponse.ToLower().Contains("incorrecto");

            return new EvaluationResult
            {
                IsCorrect = containsCorrect && !containsIncorrect,
                Confidence = 0.6,
                CorrectAnswerText = pregunta.RespuestaModelo ?? "Ver explicación",
                Explanation = aiResponse
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parseando respuesta de IA");
            return new EvaluationResult
            {
                IsCorrect = false,
                Confidence = 0.3,
                CorrectAnswerText = pregunta.RespuestaModelo ?? "Ver explicación",
                Explanation = "Error al evaluar la respuesta. Por favor, revisa el material."
            };
        }
    }

    // ═══════════════════════════════════════════════════════════
    // MÉTODOS DE EVALUACIÓN TRADICIONAL (para preguntas escritas)
    // ═══════════════════════════════════════════════════════════
    private async Task<EvaluationResult> EvaluateMultipleChoiceAsync(
        string transcription,
        PreguntasGenerada pregunta,
        int preguntaId)
    {
        try
        {
            var connection = _testRepository.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var opciones = new List<(char Letra, string Texto, bool EsCorrecta)>();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = @"
                SELECT opcion, texto_opcion, es_correcta 
                FROM pregunta_opciones 
                WHERE pregunta_generada_id = $1
                ORDER BY opcion";

                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = preguntaId });

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    opciones.Add((
                        reader.GetChar(0),
                        reader.GetString(1),
                        reader.GetBoolean(2)
                    ));
                }
            }

            if (!opciones.Any())
            {
                _logger.LogWarning("No se encontraron opciones para pregunta {PreguntaId}", preguntaId);
                return new EvaluationResult
                {
                    IsCorrect = false,
                    CorrectAnswerText = "No disponible",
                    Confidence = 0
                };
            }

            var normalizedTranscription = NormalizeText(transcription);

            var bestMatch = opciones
                .Select(o => new
                {
                    Opcion = o,
                    Score = CalculateSimilarity(normalizedTranscription, NormalizeText(o.Texto))
                })
                .OrderByDescending(x => x.Score)
                .First();

            bool mentionedLetter = normalizedTranscription.Contains($"opcion {bestMatch.Opcion.Letra.ToString().ToLower()}") ||
                                  normalizedTranscription.Contains($"letra {bestMatch.Opcion.Letra.ToString().ToLower()}") ||
                                  normalizedTranscription.Contains(bestMatch.Opcion.Letra.ToString().ToLower());

            bool isCorrect = (mentionedLetter && bestMatch.Score > 0.3) || bestMatch.Score > 0.6;

            if (isCorrect)
            {
                isCorrect = bestMatch.Opcion.EsCorrecta;
            }

            var correctOption = opciones.First(o => o.EsCorrecta);

            return new EvaluationResult
            {
                IsCorrect = isCorrect,
                CorrectAnswerText = $"Opción {correctOption.Letra}: {correctOption.Texto}",
                Confidence = bestMatch.Score
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluando selección múltiple");
            return new EvaluationResult
            {
                IsCorrect = false,
                CorrectAnswerText = "Error en evaluación",
                Confidence = 0
            };
        }
    }

    private EvaluationResult EvaluateTrueFalse(string transcription, PreguntasGenerada pregunta)
    {
        var normalized = NormalizeText(transcription);

        var trueKeywords = new[] { "verdadero", "verdad", "cierto", "correcto", "si", "afirmativo" };
        var falseKeywords = new[] { "falso", "incorrecto", "no", "negativo", "mentira" };

        var trueScore = trueKeywords.Count(k => normalized.Contains(k));
        var falseScore = falseKeywords.Count(k => normalized.Contains(k));

        bool userAnsweredTrue = trueScore > falseScore;
        bool correctAnswer = pregunta.RespuestaCorrectaBoolean ?? false;
        bool isCorrect = userAnsweredTrue == correctAnswer;

        double confidence = Math.Max(trueScore, falseScore) > 0 ? 0.8 : 0.3;

        return new EvaluationResult
        {
            IsCorrect = isCorrect,
            CorrectAnswerText = correctAnswer ? "Verdadero" : "Falso",
            Confidence = confidence
        };
    }

    // ═══════════════════════════════════════════════════════════
    // MÉTODOS AUXILIARES
    // ═══════════════════════════════════════════════════════════
    private async Task SaveOrUpdateAnswerAsync(
        int testId,
        int preguntaGeneradaId,
        short numeroOrden,
        string respuestaTexto,
        int? tiempoRespuestaSegundos = null)
    {
        var ahora = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

        var testPregunta = await _testRepository.GetTestPreguntaAsync(testId, numeroOrden);

        if (testPregunta == null)
        {
            testPregunta = new TestPregunta
            {
                TestId = testId,
                PreguntaGeneradaId = preguntaGeneradaId,
                NumeroOrden = numeroOrden,
                RespuestaTexto = respuestaTexto,
                TiempoRespuestaSegundos = tiempoRespuestaSegundos,
                FechaRespuesta = ahora
            };

            await _testRepository.CreateTestPreguntaAsync(testPregunta);
            _logger.LogInformation("🆕 Nueva respuesta creada con tiempo: {Tiempo}s", tiempoRespuestaSegundos);
        }
        else
        {
            testPregunta.RespuestaTexto = respuestaTexto;
            testPregunta.TiempoRespuestaSegundos = tiempoRespuestaSegundos;
            testPregunta.FechaRespuesta = ahora;

            await _testRepository.UpdateTestPreguntaAsync(testPregunta);
            _logger.LogInformation("🔄 Respuesta actualizada con tiempo: {Tiempo}s", tiempoRespuestaSegundos);
        }

        try
        {
            var pregunta = await _preguntaRepository.GetByIdAsync(preguntaGeneradaId);
            if (pregunta != null)
            {
                pregunta.VecesUtilizada = (pregunta.VecesUtilizada ?? 0) + 1;
                pregunta.UltimoUso = ahora;
                pregunta.FechaActualizacion = ahora;
                await _preguntaRepository.UpdateAsync(pregunta);
                _logger.LogInformation("📊 Estadísticas actualizadas");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "⚠️ No se actualizaron estadísticas");
        }
    }

    private string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        return text.ToLower()
            .Replace("á", "a").Replace("é", "e").Replace("í", "i")
            .Replace("ó", "o").Replace("ú", "u").Replace("ñ", "n")
            .Trim();
    }

    private double CalculateSimilarity(string text1, string text2)
    {
        if (string.IsNullOrWhiteSpace(text1) || string.IsNullOrWhiteSpace(text2))
            return 0;

        var words1 = text1.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var words2 = text2.Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var commonWords = words1.Intersect(words2).Count();
        var totalWords = Math.Max(words1.Length, words2.Length);

        return totalWords > 0 ? (double)commonWords / totalWords : 0;
    }

    private string GenerateFeedback(
        bool isCorrect,
        double confidence,
        PreguntasGenerada pregunta,
        bool isOralMode)
    {
        string modeIndicator = isOralMode ? "🎤" : "✍️";

        if (isCorrect)
        {
            return confidence > 0.8
                ? $"{modeIndicator} ¡Excelente! Tu respuesta es correcta y muy clara."
                : $"{modeIndicator} Tu respuesta es correcta.";
        }
        else
        {
            var feedback = $"{modeIndicator} Tu respuesta no es correcta. ";

            if (!string.IsNullOrEmpty(pregunta.Explicacion))
            {
                feedback += pregunta.Explicacion;
            }
            else
            {
                feedback += "Revisa el material de estudio para comprender mejor este concepto.";
            }

            if (isOralMode)
            {
                feedback += " Intenta reformular tu respuesta enfocándote en los conceptos clave.";
            }

            return feedback;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // CLASES DE SOPORTE
    // ═══════════════════════════════════════════════════════════
    public class EvaluateOralAnswerRequest
    {
        public int TestId { get; set; }
        public int PreguntaGeneradaId { get; set; }
        public short NumeroOrden { get; set; }
        public string Transcription { get; set; } = string.Empty;
    }

    public class EvaluationResult
    {
        public bool IsCorrect { get; set; }
        public string CorrectAnswerText { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string? Explanation { get; set; }
    }

    // 🆕 DTO para parsear respuesta de IA
    private class AIEvaluationResponse
    {
        public bool IsCorrect { get; set; }
        public double Confidence { get; set; }
        public string Evaluation { get; set; } = string.Empty;
        public List<string> KeyPointsCovered { get; set; } = new();
        public List<string> KeyPointsMissing { get; set; } = new();
        public string Feedback { get; set; } = string.Empty;
    }
}