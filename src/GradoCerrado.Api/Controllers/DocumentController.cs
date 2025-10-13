// src/GradoCerrado.Api/Controllers/DocumentController.cs
using GradoCerrado.Application.Interfaces;
using GradoCerrado.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using GradoCerrado.Infrastructure.Services;
using Microsoft.Extensions.Logging;

namespace GradoCerrado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DocumentController : ControllerBase
{
    private readonly ILogger<DocumentController> _logger;
    private readonly IVectorService _vectorService;
    private readonly IDocumentProcessingService _documentProcessing;
    private readonly ITextChunkingService _textChunking;
    private readonly IQuestionGenerationService _questionGeneration;
    private readonly IQuestionPersistenceService _questionPersistence;
    private readonly IEmbeddingService _embeddingService;
    private readonly IDocumentExtractionService _documentExtraction;
    private readonly IMetadataBuilderService _metadataBuilder;
    private readonly IContentClassifierService _contentClassifier;

    public DocumentController(
        ILogger<DocumentController> logger,
        IVectorService vectorService,
        IDocumentProcessingService documentProcessing,
        ITextChunkingService textChunking,
        IQuestionGenerationService questionGeneration,
        IQuestionPersistenceService questionPersistence,
        IDocumentExtractionService documentExtraction,
        IMetadataBuilderService metadataBuilder,
        IContentClassifierService contentClassifier,
        IEmbeddingService embeddingService)
    {
        _logger = logger;
        _vectorService = vectorService;
        _documentProcessing = documentProcessing;
        _textChunking = textChunking;
        _questionGeneration = questionGeneration;
        _questionPersistence = questionPersistence;
        _documentExtraction = documentExtraction;
        _metadataBuilder = metadataBuilder;
        _contentClassifier = contentClassifier;
        _embeddingService = embeddingService;
    }

    // 📁 src/GradoCerrado.Api/Controllers/DocumentController.cs
    // MÉTODO COMPLETO Y OPTIMIZADO

    [HttpPost("upload")]
    public async Task<ActionResult<EnhancedDocumentUploadResponse>> UploadDocument(
        IFormFile file,
        [FromQuery] int? areaId = null,
        [FromQuery] int? totalQuestions = null)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            // ════════════════════════════════════════════════════════
            // 1️⃣ VALIDACIONES
            // ════════════════════════════════════════════════════════

            if (file == null || file.Length == 0)
            {
                return BadRequest(new EnhancedDocumentUploadResponse
                {
                    Success = false,
                    Message = "No se ha enviado ningún archivo"
                });
            }

            var allowedTypes = new[] { ".txt", ".pdf", ".docx", ".md" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedTypes.Contains(fileExtension))
            {
                return BadRequest(new EnhancedDocumentUploadResponse
                {
                    Success = false,
                    Message = $"Tipo de archivo no soportado. Tipos permitidos: {string.Join(", ", allowedTypes)}"
                });
            }

            _logger.LogInformation(
                "📄 Iniciando procesamiento: {FileName} ({Size} KB, {Type})",
                file.FileName, file.Length / 1024, fileExtension);

            // ════════════════════════════════════════════════════════
            // 2️⃣ EXTRAER CONTENIDO
            // ════════════════════════════════════════════════════════

            using var stream = file.OpenReadStream();
            string content = await _documentExtraction.ExtractTextFromFileAsync(stream, file.FileName);

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest(new EnhancedDocumentUploadResponse
                {
                    Success = false,
                    Message = "El archivo está vacío o no se pudo extraer el contenido"
                });
            }

            _logger.LogInformation(
                "✅ Contenido extraído: {Chars} caracteres (~{Pages} páginas)",
                content.Length, content.Length / 3000);

            // ════════════════════════════════════════════════════════
            // 3️⃣ PROCESAR DOCUMENTO (AI: clasificación, conceptos, etc)
            // ════════════════════════════════════════════════════════

            var document = await _documentProcessing.ProcessDocumentAsync(
                content,
                file.FileName);

            _logger.LogInformation(
                "📋 Documento procesado: Tipo={Type}, Dificultad={Difficulty}, Áreas={Areas}",
                document.DocumentType, document.Difficulty, string.Join(", ", document.LegalAreas));

            // ════════════════════════════════════════════════════════
            // 4️⃣ CREAR CHUNKS
            // ════════════════════════════════════════════════════════

            var chunks = await _textChunking.CreateChunksAsync(
                content,
                maxChunkSize: 500,
                overlap: 100);

            _logger.LogInformation(
                "✂️ {Count} chunks creados (promedio: {Avg} caracteres por chunk)",
                chunks.Count, chunks.Any() ? (int)chunks.Average(c => c.Length) : 0);

            // ════════════════════════════════════════════════════════
            // 5️⃣ PREPARAR METADATA
            // ════════════════════════════════════════════════════════

            var fileInfo = new GradoCerrado.Infrastructure.Services.FileInfo
            {
                FileName = file.FileName,
                FileSize = file.Length,
                ContentType = file.ContentType
            };

            var baseMetadata = _metadataBuilder.BuildFromFile(document, fileInfo);
            var chunkMetadataList = _metadataBuilder.BuildChunkMetadataList(
                baseMetadata,
                chunks.Count,
                document.Id);

            // ════════════════════════════════════════════════════════
            // 6️⃣ VECTORIZACIÓN BATCH (OPTIMIZADA)
            // ════════════════════════════════════════════════════════

            _logger.LogInformation(
                "🔢 INICIANDO VECTORIZACIÓN BATCH de {Count} chunks...",
                chunks.Count);

            var vectorizationStart = DateTime.UtcNow;

            // ✅ UNA (o pocas) llamadas para TODOS los embeddings
            var embeddings = await _embeddingService.GenerateEmbeddingsAsync(chunks);

            if (embeddings.Count != chunks.Count)
            {
                throw new InvalidOperationException(
                    $"Error en vectorización: se esperaban {chunks.Count} embeddings pero se obtuvieron {embeddings.Count}");
            }

            // Guardar cada chunk con su embedding en Qdrant
            var chunkIds = new List<string>();

            for (int i = 0; i < chunks.Count; i++)
            {
                var vectorId = await _vectorService.AddDocumentAsync(
                    chunks[i],
                    chunkMetadataList[i],
                    embeddings[i]); // ✅ Embedding pre-calculado

                chunkIds.Add(vectorId);

                // Log de progreso cada 10 chunks
                if ((i + 1) % 10 == 0 || i == chunks.Count - 1)
                {
                    _logger.LogDebug(
                        "💾 Guardados {Current}/{Total} chunks en Qdrant",
                        i + 1, chunks.Count);
                }
            }

            var vectorizationTime = DateTime.UtcNow - vectorizationStart;

            _logger.LogInformation(
                "✅ Vectorización completa en {Seconds}s ({EmbeddingCalls} llamada(s) a OpenAI para embeddings)",
                vectorizationTime.TotalSeconds,
                Math.Ceiling(chunks.Count / 100.0)); // Batch de ~100

            // ════════════════════════════════════════════════════════
            // 7️⃣ CALCULAR CANTIDAD DE PREGUNTAS
            // ════════════════════════════════════════════════════════

            int questionsToGenerate;

            if (totalQuestions.HasValue)
            {
                questionsToGenerate = totalQuestions.Value;
                _logger.LogInformation(
                    "👤 Cantidad manual especificada: {Count} preguntas",
                    questionsToGenerate);
            }
            else
            {
                questionsToGenerate = CalculateOptimalQuestionCount(content.Length, chunks.Count);
                _logger.LogInformation(
                    "🤖 Cantidad calculada automáticamente: {Count} preguntas para {Chars} caracteres",
                    questionsToGenerate, content.Length);
            }

            // ════════════════════════════════════════════════════════
            // 8️⃣ GENERAR PREGUNTAS (CON RATE LIMITING)
            // ════════════════════════════════════════════════════════

            _logger.LogInformation(
                "🤖 INICIANDO GENERACIÓN de {Count} preguntas con TODOS los niveles (esto puede tomar tiempo)...",
                questionsToGenerate);

            var questionStart = DateTime.UtcNow;

            var generatedQuestions = await _questionGeneration.GenerateQuestionsWithMixedDifficulty(
                document,
                questionsToGenerate);

            var questionTime = DateTime.UtcNow - questionStart;

            _logger.LogInformation(
                "✅ {Count} preguntas generadas en {Seconds}s",
                generatedQuestions.Count, questionTime.TotalSeconds);

            // ════════════════════════════════════════════════════════
            // 9️⃣ ASIGNAR CHUNKS A PREGUNTAS (TRAZABILIDAD)
            // ════════════════════════════════════════════════════════

            AssignChunksToQuestions(generatedQuestions, chunkIds);

            _logger.LogInformation(
                "🔗 Trazabilidad establecida: chunks asignados a {Count} preguntas",
                generatedQuestions.Count);

            // ════════════════════════════════════════════════════════
            // 🔟 CLASIFICAR Y GUARDAR EN BASE DE DATOS
            // ════════════════════════════════════════════════════════

            int finalAreaId = areaId ?? await _questionPersistence.GetAreaIdByName(
                document.LegalAreas.FirstOrDefault() ?? "General");

            // 🆕 CLASIFICAR CONTENIDO EN TEMAS/SUBTEMAS EXISTENTES
            _logger.LogInformation("🔍 Clasificando contenido en temas existentes...");
            var classifications = await _contentClassifier.ClassifyContentAsync(content, finalAreaId);

            List<int> savedQuestionIds;
            List<ClassificationInfo> classificationInfoList;

            if (!classifications.Any())
            {
                // Fallback: usar método tradicional si no hay clasificaciones
                _logger.LogWarning("⚠️ No se pudo clasificar automáticamente. Usando método tradicional...");
                
                int temaId = await _questionPersistence.GetOrCreateTemaId(
                    document.Title,
                    finalAreaId);

                savedQuestionIds = await _questionPersistence.SaveQuestionsToDatabase(
                    generatedQuestions,
                    temaId,
                    subtemaId: null,
                    modalidadId: 1,
                    creadaPor: $"AI-Document-{document.Id}");

                classificationInfoList = new List<ClassificationInfo>
                {
                    new ClassificationInfo
                    {
                        TemaId = temaId,
                        TemaNombre = document.Title,
                        SubtemaId = null,
                        SubtemaNombre = null,
                        Confidence = 1.0
                    }
                };
            }
            else
            {
                // Usar clasificación automática
                var mainClassification = classifications.First();
                _logger.LogInformation(
                    "✅ Contenido clasificado: Tema='{Tema}', Subtema='{Subtema}', Confianza={Conf:P0}",
                    mainClassification.TemaNombre,
                    mainClassification.SubtemaNombre ?? "N/A",
                    mainClassification.Confidence);

                // 🆕 DISTRIBUIR PREGUNTAS ENTRE CLASIFICACIONES
                savedQuestionIds = new List<int>();
                int questionsPerClassification = generatedQuestions.Count / classifications.Count;
                int remainingQuestions = generatedQuestions.Count % classifications.Count;

                for (int i = 0; i < classifications.Count; i++)
                {
                    var classification = classifications[i];
                    int startIndex = i * questionsPerClassification;
                    int count = questionsPerClassification + (i == 0 ? remainingQuestions : 0);

                    var questionsForThisTema = generatedQuestions.Skip(startIndex).Take(count).ToList();

                    if (questionsForThisTema.Any())
                    {
                        var ids = await _questionPersistence.SaveQuestionsToDatabase(
                            questionsForThisTema,
                            classification.TemaId,
                            subtemaId: classification.SubtemaId,
                            modalidadId: 1,
                            creadaPor: $"AI-Document-{document.Id}"
                        );

                        savedQuestionIds.AddRange(ids);

                        _logger.LogInformation(
                            "💾 {Count} preguntas guardadas en Tema '{Tema}' {Subtema}",
                            ids.Count,
                            classification.TemaNombre,
                            classification.SubtemaNombre != null ? $"/ Subtema '{classification.SubtemaNombre}'" : "");
                    }
                }

                classificationInfoList = classifications.Select(c => new ClassificationInfo
                {
                    TemaId = c.TemaId,
                    TemaNombre = c.TemaNombre,
                    SubtemaId = c.SubtemaId,
                    SubtemaNombre = c.SubtemaNombre,
                    Confidence = c.Confidence
                }).ToList();
            }

            // ════════════════════════════════════════════════════════
            // 1️⃣1️⃣ PREPARAR RESUMEN
            // ════════════════════════════════════════════════════════

            var breakdown = generatedQuestions
                .GroupBy(q => q.Difficulty)
                .ToDictionary(g => g.Key, g => g.Count());

            var totalTime = DateTime.UtcNow - startTime;

            // ════════════════════════════════════════════════════════
            // 1️⃣2️⃣ LOGGING FINAL
            // ════════════════════════════════════════════════════════

            _logger.LogInformation(@"
╔══════════════════════════════════════════════════════════════╗
║                  RESUMEN DE PROCESAMIENTO                    ║
╠══════════════════════════════════════════════════════════════╣
║  📄 Documento: {0,-49} ║
║  📏 Tamaño: {1,-52} ║
║  ✂️  Chunks creados: {2,-46} ║
║  🔢 Vectorización: {3,-47} ║
║  🤖 Generación preguntas: {4,-40} ║
║                                                              ║
║  📊 PREGUNTAS GENERADAS:                                     ║
║     • Básicas: {5,-49} ║
║     • Intermedias: {6,-45} ║
║     • Avanzadas: {7,-47} ║
║     • TOTAL: {8,-51} ║
║                                                              ║
║  🎯 Clasificadas en: {9,-45} ║
║  ⏱️  TIEMPO TOTAL: {10,-47} ║
║  🎯 API Calls: ~{11,-48} ║
╚══════════════════════════════════════════════════════════════╝",
                file.FileName,
                $"{file.Length / 1024} KB",
                chunks.Count,
                $"{vectorizationTime.TotalSeconds:F1}s",
                $"{questionTime.TotalSeconds:F1}s",
                breakdown.GetValueOrDefault(DifficultyLevel.Basic, 0),
                breakdown.GetValueOrDefault(DifficultyLevel.Intermediate, 0),
                breakdown.GetValueOrDefault(DifficultyLevel.Advanced, 0),
                savedQuestionIds.Count,
                $"{classificationInfoList.Count} tema(s)",
                $"{totalTime.TotalSeconds:F1}s",
                $"{Math.Ceiling(chunks.Count / 100.0) + generatedQuestions.Count} llamadas");

            // ════════════════════════════════════════════════════════
            // 1️⃣3️⃣ RETORNAR RESPUESTA
            // ════════════════════════════════════════════════════════

            return Ok(new EnhancedDocumentUploadResponse
            {
                Success = true,
                Document = document,
                ChunkIds = chunkIds,
                ChunksCreated = chunks.Count,
                TotalCharacters = content.Length,
                SampleQuestions = generatedQuestions.Take(5).ToList(),
                QuestionsGeneratedCount = savedQuestionIds.Count,
                SavedQuestionIds = savedQuestionIds,
                ProcessingTimeMs = (int)totalTime.TotalMilliseconds,
                QuestionBreakdown = new QuestionDifficultyBreakdown
                {
                    Basico = breakdown.GetValueOrDefault(DifficultyLevel.Basic, 0),
                    Intermedio = breakdown.GetValueOrDefault(DifficultyLevel.Intermediate, 0),
                    Avanzado = breakdown.GetValueOrDefault(DifficultyLevel.Advanced, 0)
                },
                Classifications = classificationInfoList,
                Message = $"✅ Procesado en {totalTime.TotalSeconds:F1}s: " +
                         $"{chunks.Count} chunks vectorizados (batch), " +
                         $"{savedQuestionIds.Count} preguntas generadas y clasificadas en {classificationInfoList.Count} tema(s)"
            });
        }
        catch (Exception ex)
        {
            var totalTime = DateTime.UtcNow - startTime;

            _logger.LogError(ex,
                "❌ Error después de {Seconds}s procesando documento: {FileName}",
                totalTime.TotalSeconds, file?.FileName ?? "unknown");

            return StatusCode(500, new EnhancedDocumentUploadResponse
            {
                Success = false,
                Message = $"Error procesando documento: {ex.Message}",
                ProcessingTimeMs = (int)totalTime.TotalMilliseconds
            });
        }
    }

    // ════════════════════════════════════════════════════════════
    // MÉTODOS AUXILIARES
    // ════════════════════════════════════════════════════════════

    /// <summary>
    /// Asigna chunks a preguntas para trazabilidad
    /// </summary>
    private void AssignChunksToQuestions(
        List<StudyQuestion> questions,
        List<string> chunkIds)
    {
        if (!chunkIds.Any() || !questions.Any())
            return;

        // Estrategia: Distribuir chunks equitativamente entre preguntas
        int chunksPerQuestion = Math.Max(1, chunkIds.Count / questions.Count);

        for (int i = 0; i < questions.Count; i++)
        {
            var startIdx = i * chunksPerQuestion;
            var endIdx = Math.Min(startIdx + chunksPerQuestion, chunkIds.Count);

            // Asignar chunks a esta pregunta
            questions[i].SourceChunkIds = chunkIds
                .Skip(startIdx)
                .Take(endIdx - startIdx)
                .ToList();

            _logger.LogDebug(
                "🔗 Pregunta {Index} asignada con {Count} chunks: {ChunkIds}",
                i + 1,
                questions[i].SourceChunkIds.Count,
                string.Join(", ", questions[i].SourceChunkIds.Select(c => c.Substring(0, 8))));
        }
    }

    /// <summary>
    /// Calcula cantidad óptima de preguntas según el tamaño del documento
    /// </summary>
    private int CalculateOptimalQuestionCount(int contentLength, int chunkCount)
    {
        // Lógica simple: 1 pregunta por cada ~500 caracteres
        // Mínimo 5, máximo 50
        var calculated = Math.Max(5, Math.Min(50, contentLength / 500));

        _logger.LogDebug(
            "📊 Cálculo de preguntas: {Length} chars, {Chunks} chunks → {Questions} preguntas",
            contentLength, chunkCount, calculated);

        return calculated;
    }

    [HttpPost("upload-text")]
    public async Task<ActionResult<EnhancedDocumentUploadResponse>> UploadTextContent([FromBody] TextUploadRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest("El contenido no puede estar vacío");
            }

            var fileName = request.Title ?? "documento_manual.txt";
            var document = await _documentProcessing.ProcessDocumentAsync(request.Content, fileName, request.DocumentType);
            var chunks = await _textChunking.CreateChunksAsync(request.Content, maxChunkSize: 1000, overlap: 200);

            var baseMetadata = _metadataBuilder.BuildFromText(document, fileName, request.Content.Length, source: "Manual");
            var chunkMetadataList = _metadataBuilder.BuildChunkMetadataList(baseMetadata, chunks.Count, document.Id);

            var chunkIds = new List<string>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var vectorId = await _vectorService.AddDocumentAsync(chunks[i], chunkMetadataList[i]);
                chunkIds.Add(vectorId);
            }

            var questionCount = request.GenerateQuestions ? (request.QuestionCount ?? 5) : 0;
            var sampleQuestions = questionCount > 0
                ? await _questionGeneration.GenerateQuestionsFromDocument(document, questionCount)
                : new List<StudyQuestion>();

            return Ok(new EnhancedDocumentUploadResponse
            {
                Success = true,
                Document = document,
                ChunkIds = chunkIds,
                ChunksCreated = chunks.Count,
                TotalCharacters = request.Content.Length,
                SampleQuestions = sampleQuestions,
                Message = $"Documento procesado exitosamente en {chunks.Count} fragmentos"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando contenido de texto");
            return StatusCode(500, new EnhancedDocumentUploadResponse
            {
                Success = false,
                Message = $"Error procesando documento: {ex.Message}"
            });
        }
    }

    [HttpPost("{documentId}/generate-questions")]
    public async Task<ActionResult> GenerateMoreQuestions(
        Guid documentId,
        [FromQuery] int count = 10,
        [FromQuery] string difficulty = "intermedio",
        [FromQuery] int? modalidadId = 1)
    {
        try
        {
            _logger.LogInformation("🤖 Generando {Count} preguntas adicionales para documento {DocId}", count, documentId);

            var document = await GetDocumentByIdAsync(documentId);

            if (document == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Documento no encontrado"
                });
            }

            var questions = await _questionGeneration.GenerateQuestionsFromDocument(document, count);

            var areaId = await _questionPersistence.GetAreaIdByName(
                document.LegalAreas.FirstOrDefault() ?? "General"
            );

            var temaId = await _questionPersistence.GetOrCreateTemaId(
                document.Title,
                areaId
            );

            var savedIds = await _questionPersistence.SaveQuestionsToDatabase(
                questions,
                temaId,
                modalidadId: modalidadId ?? 1,
                creadaPor: $"AI-Additional-{documentId}"
            );

            _logger.LogInformation("✅ {Count} preguntas adicionales guardadas", savedIds.Count);

            return Ok(new
            {
                success = true,
                documentId = documentId,
                questionsGenerated = savedIds.Count,
                savedQuestionIds = savedIds,
                message = $"{savedIds.Count} preguntas generadas y guardadas exitosamente"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generando preguntas adicionales");
            return StatusCode(500, new
            {
                success = false,
                message = $"Error: {ex.Message}"
            });
        }
    }

    [HttpPost("test-document")]
    public async Task<ActionResult<EnhancedDocumentUploadResponse>> TestWithSampleDocument()
    {
        try
        {
            var sampleContent = @"
ARTÍCULO 19 DE LA CONSTITUCIÓN POLÍTICA DE CHILE

El artículo 19 de la Constitución Política de la República de Chile garantiza a todas las personas los siguientes derechos:

1. El derecho a la vida y a la integridad física y psíquica de la persona.
2. La igualdad ante la ley, estableciendo que ni la ley ni autoridad alguna podrán establecer diferencias arbitrarias.
3. La igual protección de la ley en el ejercicio de sus derechos.
4. El respeto y protección a la vida privada y a la honra de la persona y su familia.
5. La inviolabilidad del hogar y de toda forma de comunicación privada.
6. La libertad de conciencia, la manifestación de todas las creencias y el ejercicio libre de todos los cultos.
7. El derecho a la libertad personal y a la seguridad individual.
8. El derecho a vivir en un medio ambiente libre de contaminación.

PRINCIPIOS FUNDAMENTALES:

- Supremacía Constitucional: La Constitución es la norma suprema del ordenamiento jurídico.
- Estado de Derecho: Todas las personas e instituciones están sujetas a la ley.
- Separación de Poderes: División del poder público en Ejecutivo, Legislativo y Judicial.

RECURSOS DE PROTECCIÓN:

El recurso de protección procede cuando se vulneran los derechos establecidos en el artículo 19, específicamente los números 1, 2, 3, 4, 5, 6, 9, 11, 12, 15, 16, 19, 21, 22, 23, 24 y 25.

JURISPRUDENCIA RELEVANTE:

- Caso Palamara Iribarne vs. Chile (2005): Sobre libertad de expresión y debido proceso.
- Caso Atala Riffo vs. Chile (2012): Sobre discriminación y vida privada.
";

            var document = await _documentProcessing.ProcessDocumentAsync(
                sampleContent,
                "articulo_19_constitucion.txt",
                LegalDocumentType.Constitution);

            var chunks = await _textChunking.CreateChunksAsync(sampleContent, maxChunkSize: 1000, overlap: 200);
            var baseMetadata = _metadataBuilder.BuildFromText(document, "articulo_19_constitucion.txt", sampleContent.Length, source: "Sample");
            var chunkMetadataList = _metadataBuilder.BuildChunkMetadataList(baseMetadata, chunks.Count, document.Id);

            var chunkIds = new List<string>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var vectorId = await _vectorService.AddDocumentAsync(chunks[i], chunkMetadataList[i]);
                chunkIds.Add(vectorId);
            }

            var questions = await _questionGeneration.GenerateQuestionsFromDocument(document, 8);

            return Ok(new EnhancedDocumentUploadResponse
            {
                Success = true,
                Document = document,
                ChunkIds = chunkIds,
                ChunksCreated = chunks.Count,
                TotalCharacters = sampleContent.Length,
                SampleQuestions = questions,
                Message = "Documento de prueba procesado exitosamente"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error con documento de prueba");
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult<DocumentSystemStatus>> GetStatus()
    {
        try
        {
            var collectionExists = await _vectorService.CollectionExistsAsync();

            return Ok(new DocumentSystemStatus
            {
                VectorDatabaseReady = collectionExists,
                Message = collectionExists ? "Sistema listo para recibir documentos" : "Base de datos vectorial no inicializada"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando estado del sistema");
            return StatusCode(500, new DocumentSystemStatus
            {
                VectorDatabaseReady = false,
                Message = $"Error verificando estado: {ex.Message}"
            });
        }
    }

    [HttpPost("generate-questions")]
    public async Task<ActionResult<List<StudyQuestion>>> GenerateQuestionsFromVector([FromBody] GenerateQuestionsRequest request)
    {
        try
        {
            if (request.LegalAreas == null || !request.LegalAreas.Any())
            {
                return BadRequest("Debe especificar al menos un área legal");
            }

            List<StudyQuestion> questions;

            if (!string.IsNullOrWhiteSpace(request.SearchQuery))
            {
                var relevantDocs = await _vectorService.SearchSimilarAsync(request.SearchQuery, 3);
                questions = await GenerateQuestionsFromSearchResults(relevantDocs, request);
            }
            else
            {
                questions = await _questionGeneration.GenerateRandomQuestions(request.LegalAreas, request.Difficulty, request.Count);
            }

            return Ok(questions);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando preguntas");
            return StatusCode(500, $"Error generando preguntas: {ex.Message}");
        }
    }

    [HttpGet("list")]
    public async Task<ActionResult<List<DocumentSummary>>> ListDocuments(
        [FromQuery] LegalDocumentType? documentType = null,
        [FromQuery] string? legalArea = null,
        [FromQuery] DifficultyLevel? difficulty = null)
    {
        try
        {
            var allResults = await _vectorService.SearchSimilarAsync("documento", 1000);

            var documentGroups = allResults
                .Where(doc => doc.Metadata.ContainsKey("document_id"))
                .GroupBy(doc => doc.Metadata["document_id"].ToString())
                .Select(group => CreateDocumentSummary(group))
                .Where(doc => doc != null)
                .Cast<DocumentSummary>()
                .ToList();

            if (documentType.HasValue)
                documentGroups = documentGroups.Where(d => d.DocumentType == documentType.Value).ToList();

            if (!string.IsNullOrEmpty(legalArea))
                documentGroups = documentGroups.Where(d => d.LegalAreas.Contains(legalArea)).ToList();

            if (difficulty.HasValue)
                documentGroups = documentGroups.Where(d => d.Difficulty == difficulty.Value).ToList();

            return Ok(documentGroups.OrderByDescending(d => d.CreatedAt).ToList());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al listar documentos");
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    [HttpPost("search")]
    public async Task<ActionResult> SearchDocuments([FromBody] DocumentSearchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest("La consulta no puede estar vacía");
            }

            var results = await _vectorService.SearchSimilarAsync(request.Query, request.Limit);

            return Ok(new
            {
                query = request.Query,
                results = results.Select(r => new
                {
                    id = r.Id,
                    content = r.Content,
                    score = r.Score,
                    metadata = r.Metadata
                }),
                count = results.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar documentos");
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    [HttpDelete("{documentId}")]
    public async Task<ActionResult> DeleteDocument(Guid documentId)
    {
        try
        {
            var allResults = await _vectorService.SearchSimilarAsync("", 1000);
            var documentChunks = allResults
                .Where(doc => doc.Metadata.ContainsKey("document_id") &&
                             doc.Metadata["document_id"].ToString() == documentId.ToString())
                .ToList();

            if (!documentChunks.Any())
            {
                return NotFound("Documento no encontrado");
            }

            int deletedCount = 0;
            foreach (var chunk in documentChunks)
            {
                var result = await _vectorService.DeleteDocumentAsync(chunk.Id);
                if (result) deletedCount++;
            }

            return Ok(new
            {
                message = $"Documento eliminado: {deletedCount}/{documentChunks.Count} chunks eliminados",
                success = deletedCount == documentChunks.Count
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar documento");
            return StatusCode(500, $"Error: {ex.Message}");
        }
    }

    [HttpPost("generate-questions-for-student/{documentId}")]
    public async Task<ActionResult<StudyQuestionsResponse>> GenerateQuestionsForStudent(
        Guid documentId,
        [FromBody] GenerateQuestionsForStudentRequest request)
    {
        try
        {
            var document = await GetDocumentByIdAsync(documentId);
            if (document == null)
            {
                return NotFound("Documento no encontrado");
            }

            var questions = await _questionGeneration.GenerateQuestionsFromDocument(document, request.QuestionCount);

            var response = new StudyQuestionsResponse
            {
                DocumentId = documentId,
                DocumentTitle = document.Title,
                StudentId = request.StudentId,
                SessionId = request.SessionId,
                Questions = questions.Select(q => new QuestionForStudent
                {
                    Id = q.Id,
                    QuestionText = q.QuestionText,
                    Options = q.Options?.Select(o => new QuestionOptionDto
                    {
                        Id = Guid.NewGuid().ToString(),
                        Text = o.Text,
                        IsCorrect = o.IsCorrect
                    }).ToList() ?? new List<QuestionOptionDto>(),
                    LegalArea = q.LegalArea,
                    Difficulty = q.Difficulty,
                    RelatedConcepts = q.RelatedConcepts
                }).ToList(),
                GeneratedAt = DateTime.UtcNow
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando preguntas para estudiante");
            return StatusCode(500, "Error interno del servidor");
        }
    }

    [HttpGet("qdrant/stats")]
    public async Task<ActionResult> ListAllQdrantDocuments()
    {
        try
        {
            var stats = await _vectorService.GetCollectionStatsAsync();

            return Ok(new
            {
                success = true,
                collectionName = "legal_documents",
                totalVectors = stats.VectorsCount,
                indexedVectors = stats.IndexedVectorsCount,
                status = stats.Status,
                message = $"✅ Qdrant tiene {stats.VectorsCount} vectores almacenados"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas de Qdrant");
            return StatusCode(500, new
            {
                success = false,
                message = $"Error: {ex.Message}"
            });
        }
    }

    private async Task<LegalDocument?> GetDocumentByIdAsync(Guid documentId)
    {
        try
        {
            var allResults = await _vectorService.SearchSimilarAsync("documento", 1000);
            var documentChunks = allResults
                .Where(doc => doc.Metadata.ContainsKey("document_id") &&
                             doc.Metadata["document_id"].ToString() == documentId.ToString())
                .ToList();

            if (!documentChunks.Any())
                return null;

            var firstChunk = documentChunks.First();
            var metadata = firstChunk.Metadata;

            return new LegalDocument
            {
                Id = documentId,
                Title = metadata.GetValueOrDefault("title", "").ToString()!,
                DocumentType = Enum.Parse<LegalDocumentType>(metadata.GetValueOrDefault("document_type", "StudyMaterial").ToString()!),
                LegalAreas = metadata.GetValueOrDefault("legal_areas", "").ToString()!.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                KeyConcepts = metadata.GetValueOrDefault("key_concepts", "").ToString()!.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                Difficulty = Enum.Parse<DifficultyLevel>(metadata.GetValueOrDefault("difficulty", "Intermediate").ToString()!),
                Source = metadata.GetValueOrDefault("source", "").ToString()!,
                CreatedAt = DateTime.Parse(metadata.GetValueOrDefault("created_at", DateTime.MinValue.ToString("O")).ToString()!),
                Content = string.Join("\n", documentChunks.Select(c => c.Content))
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo documento {DocumentId}", documentId);
            return null;
        }
    }

    private DocumentSummary? CreateDocumentSummary(IGrouping<string?, SearchResult> group)
    {
        try
        {
            var firstDoc = group.First();
            var metadata = firstDoc.Metadata;

            return new DocumentSummary
            {
                DocumentId = Guid.Parse(metadata["document_id"].ToString()!),
                Title = metadata.GetValueOrDefault("title", "").ToString()!,
                DocumentType = Enum.Parse<LegalDocumentType>(metadata.GetValueOrDefault("document_type", "StudyMaterial").ToString()!),
                LegalAreas = metadata.GetValueOrDefault("legal_areas", "").ToString()!.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                Difficulty = Enum.Parse<DifficultyLevel>(metadata.GetValueOrDefault("difficulty", "Intermediate").ToString()!),
                CreatedAt = DateTime.Parse(metadata.GetValueOrDefault("created_at", DateTime.MinValue.ToString("O")).ToString()!),
                FileName = metadata.GetValueOrDefault("file_name", "").ToString()!,
                ChunkCount = group.Count(),
                FileSize = long.Parse(metadata.GetValueOrDefault("file_size", "0").ToString()!)
            };
        }
        catch
        {
            return null;
        }
    }

    private async Task<List<StudyQuestion>> GenerateQuestionsFromSearchResults(
        List<SearchResult> searchResults,
        GenerateQuestionsRequest request)
    {
        if (!searchResults.Any())
        {
            return new List<StudyQuestion>();
        }

        var combinedContent = string.Join("\n\n", searchResults.Select(r => r.Content));
        var tempDocument = new LegalDocument
        {
            Id = Guid.NewGuid(),
            Title = $"Resultados para: {request.SearchQuery}",
            Content = combinedContent,
            LegalAreas = request.LegalAreas,
            Difficulty = request.Difficulty,
            DocumentType = LegalDocumentType.StudyMaterial,
            CreatedAt = DateTime.UtcNow
        };

        return await _questionGeneration.GenerateQuestionsFromDocument(tempDocument, request.Count);
    }
}

// ═══════════════════════════════════════════════════════════════════
// DTOs Y CLASES DE RESPUESTA
// ═══════════════════════════════════════════════════════════════════

public class EnhancedDocumentUploadResponse
{
    public bool Success { get; set; }
    public LegalDocument? Document { get; set; }
    public List<string> ChunkIds { get; set; } = new();
    public int ChunksCreated { get; set; }
    public int TotalCharacters { get; set; }
    public int QuestionsGeneratedCount { get; set; }
    public List<int> SavedQuestionIds { get; set; } = new();
    public List<StudyQuestion> SampleQuestions { get; set; } = new();
    public QuestionDifficultyBreakdown? QuestionBreakdown { get; set; }
    public List<ClassificationInfo> Classifications { get; set; } = new();
    public int? ProcessingTimeMs { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class QuestionDifficultyBreakdown
{
    public int Basico { get; set; }
    public int Intermedio { get; set; }
    public int Avanzado { get; set; }

    public int Total => Basico + Intermedio + Avanzado;

    public BreakdownPercentages Percentages => new()
    {
        BasicoPercent = Total > 0 ? Math.Round((decimal)Basico / Total * 100, 1) : 0,
        IntermedioPercent = Total > 0 ? Math.Round((decimal)Intermedio / Total * 100, 1) : 0,
        AvanzadoPercent = Total > 0 ? Math.Round((decimal)Avanzado / Total * 100, 1) : 0
    };
}

public class BreakdownPercentages
{
    public decimal BasicoPercent { get; set; }
    public decimal IntermedioPercent { get; set; }
    public decimal AvanzadoPercent { get; set; }
}

/// <summary>
/// Información de clasificación de contenido en temas
/// </summary>
public class ClassificationInfo
{
    public int TemaId { get; set; }
    public string TemaNombre { get; set; } = string.Empty;
    public int? SubtemaId { get; set; }
    public string? SubtemaNombre { get; set; }
    public double Confidence { get; set; }
}

public class TextUploadRequest
{
    public string Content { get; set; } = string.Empty;
    public string? Title { get; set; }
    public LegalDocumentType? DocumentType { get; set; }
    public bool GenerateQuestions { get; set; } = true;
    public int? QuestionCount { get; set; } = 5;
}

public class DocumentSummary
{
    public Guid DocumentId { get; set; }
    public string Title { get; set; } = string.Empty;
    public LegalDocumentType DocumentType { get; set; }
    public List<string> LegalAreas { get; set; } = new();
    public DifficultyLevel Difficulty { get; set; }
    public DateTime CreatedAt { get; set; }
    public string FileName { get; set; } = string.Empty;
    public int ChunkCount { get; set; }
    public long FileSize { get; set; }
}

public class DocumentSearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 10;
}

public class DocumentSystemStatus
{
    public bool VectorDatabaseReady { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class GenerateQuestionsRequest
{
    public List<string> LegalAreas { get; set; } = new();
    public DifficultyLevel Difficulty { get; set; } = DifficultyLevel.Intermediate;
    public int Count { get; set; } = 5;
    public string? SearchQuery { get; set; }
}

public class GenerateQuestionsForStudentRequest
{
    public Guid StudentId { get; set; }
    public Guid? SessionId { get; set; }
    public int QuestionCount { get; set; } = 5;
}

public class StudyQuestionsResponse
{
    public Guid DocumentId { get; set; }
    public string DocumentTitle { get; set; } = string.Empty;
    public Guid StudentId { get; set; }
    public Guid? SessionId { get; set; }
    public List<QuestionForStudent> Questions { get; set; } = new();
    public DateTime GeneratedAt { get; set; }
}

public class QuestionForStudent
{
    public Guid Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public List<QuestionOptionDto> Options { get; set; } = new();
    public string LegalArea { get; set; } = string.Empty;
    public DifficultyLevel Difficulty { get; set; }
    public List<string> RelatedConcepts { get; set; } = new();
}

public class QuestionOptionDto
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}