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
    private readonly IDocumentProcessingService _documentProcessing;
    private readonly IVectorService _vectorService;
    private readonly IQuestionGenerationService _questionGeneration;
    private readonly ILogger<DocumentController> _logger;
    private readonly ITextChunkingService _textChunking;
    private readonly IMetadataBuilderService _metadataBuilder;
    private readonly IDocumentExtractionService _documentExtraction;
    private readonly IQuestionPersistenceService _questionPersistence;

    public DocumentController(
        IDocumentProcessingService documentProcessing,
        IVectorService vectorService,
        IQuestionGenerationService questionGeneration,
        ILogger<DocumentController> logger,
        IDocumentExtractionService documentExtraction,
        ITextChunkingService textChunking,
        IMetadataBuilderService metadataBuilder,
        IQuestionPersistenceService questionPersistence)
    {
        _documentProcessing = documentProcessing;
        _vectorService = vectorService;
        _questionPersistence = questionPersistence;
        _logger = logger;
        _documentExtraction = documentExtraction;
        _textChunking = textChunking;
        _metadataBuilder = metadataBuilder;
        _questionGeneration = questionGeneration;
    }

    [HttpPost("upload")]
    public async Task<ActionResult<EnhancedDocumentUploadResponse>> UploadDocument(
        IFormFile file,
        [FromQuery] int? areaId = null,
        [FromQuery] int? totalQuestions = null)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest("No se ha enviado ningún archivo");
            }

            var allowedTypes = new[] { ".txt", ".pdf", ".docx", ".md" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();

            if (!allowedTypes.Contains(fileExtension))
            {
                return BadRequest($"Tipo de archivo no soportado. Tipos permitidos: {string.Join(", ", allowedTypes)}");
            }

            // 1️⃣ EXTRAER CONTENIDO
            using var stream = file.OpenReadStream();
            string content = await _documentExtraction.ExtractTextFromFileAsync(stream, file.FileName);

            if (string.IsNullOrWhiteSpace(content))
            {
                return BadRequest("El archivo está vacío o no se pudo extraer el contenido");
            }

            _logger.LogInformation("📄 Procesando documento: {FileName}", file.FileName);

            // 2️⃣ PROCESAR DOCUMENTO
            var document = await _documentProcessing.ProcessDocumentAsync(content, file.FileName);

            // 3️⃣ CREAR CHUNKS
            var chunks = await _textChunking.CreateChunksAsync(content, maxChunkSize: 500, overlap: 100);

            // 4️⃣ VECTORIZAR
            var fileInfo = new GradoCerrado.Infrastructure.Services.FileInfo
            {
                FileName = file.FileName,
                FileSize = file.Length,
                ContentType = file.ContentType
            };

            var baseMetadata = _metadataBuilder.BuildFromFile(document, fileInfo);
            var chunkMetadataList = _metadataBuilder.BuildChunkMetadataList(baseMetadata, chunks.Count, document.Id);

            var chunkIds = new List<string>();
            for (int i = 0; i < chunks.Count; i++)
            {
                var vectorId = await _vectorService.AddDocumentAsync(chunks[i], chunkMetadataList[i]);
                chunkIds.Add(vectorId);
                _logger.LogInformation("📦 Chunk {Index}/{Total} vectorizado: {VectorId}", i + 1, chunks.Count, vectorId);
            }

            // 5️⃣ CALCULAR CANTIDAD INTELIGENTE DE PREGUNTAS
            int questionsToGenerate;
            if (totalQuestions.HasValue)
            {
                questionsToGenerate = totalQuestions.Value;
                _logger.LogInformation("👤 Cantidad manual: {Count} preguntas", questionsToGenerate);
            }
            else
            {
                questionsToGenerate = CalculateOptimalQuestionCount(content.Length, chunks.Count);
                _logger.LogInformation("🤖 Cantidad calculada: {Count} preguntas para {Chars} caracteres",
                    questionsToGenerate, content.Length);
            }

            // 6️⃣ GENERAR PREGUNTAS CON DISTRIBUCIÓN DE NIVELES
            _logger.LogInformation("🤖 Generando {Count} preguntas con TODOS los niveles...", questionsToGenerate);

            var generatedQuestions = await _questionGeneration.GenerateQuestionsWithMixedDifficulty(
                document,
                questionsToGenerate
            );

            // 🆕 7️⃣ ASIGNAR CHUNKS A PREGUNTAS (TRAZABILIDAD)
            AssignChunksToQuestions(generatedQuestions, chunkIds);

            _logger.LogInformation("✅ Chunks asignados a {Count} preguntas", generatedQuestions.Count);

            // 8️⃣ GUARDAR EN BD
            int finalAreaId = areaId ?? await _questionPersistence.GetAreaIdByName(
                document.LegalAreas.FirstOrDefault() ?? "General"
            );

            int temaId = await _questionPersistence.GetOrCreateTemaId(
                document.Title,
                finalAreaId
            );

            var savedQuestionIds = await _questionPersistence.SaveQuestionsToDatabase(
                generatedQuestions,
                temaId,
                subtemaId: null,
                modalidadId: 1,
                creadaPor: $"AI-Document-{document.Id}"
            );

            // 9️⃣ LOGS DE RESUMEN
            var breakdown = generatedQuestions.GroupBy(q => q.Difficulty)
                .ToDictionary(g => g.Key, g => g.Count());

            _logger.LogInformation(
                "✅ {Total} preguntas guardadas - Básico: {Basic}, Intermedio: {Inter}, Avanzado: {Adv}",
                savedQuestionIds.Count,
                breakdown.GetValueOrDefault(DifficultyLevel.Basic, 0),
                breakdown.GetValueOrDefault(DifficultyLevel.Intermediate, 0),
                breakdown.GetValueOrDefault(DifficultyLevel.Advanced, 0)
            );

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
                QuestionBreakdown = new QuestionDifficultyBreakdown
                {
                    Basico = breakdown.GetValueOrDefault(DifficultyLevel.Basic, 0),
                    Intermedio = breakdown.GetValueOrDefault(DifficultyLevel.Intermediate, 0),
                    Avanzado = breakdown.GetValueOrDefault(DifficultyLevel.Advanced, 0)
                },
                Message = $"Documento procesado: {chunks.Count} chunks, {savedQuestionIds.Count} preguntas generadas con trazabilidad"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error procesando documento");
            return StatusCode(500, new EnhancedDocumentUploadResponse
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            });
        }
    }

    // 🆕 MÉTODO AUXILIAR: Asignar chunks a preguntas
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
                "Pregunta {Index} asignada con {Count} chunks: {ChunkIds}",
                i + 1,
                questions[i].SourceChunkIds.Count,
                string.Join(", ", questions[i].SourceChunkIds.Select(c => c.Substring(0, 8))));
        }
    }

    // MÉTODO EXISTENTE (mantener como está)
    private int CalculateOptimalQuestionCount(int contentLength, int chunkCount)
    {
        // Lógica simple: 1 pregunta por cada ~500 caracteres
        // Mínimo 5, máximo 50
        var calculated = Math.Max(5, Math.Min(50, contentLength / 500));

        _logger.LogInformation(
            "Calculando preguntas: {Length} chars, {Chunks} chunks → {Questions} preguntas",
            contentLength, chunkCount, calculated);

        return calculated;
    }


    public class EnhancedDocumentUploadResponse
    {
        public bool Success { get; set; }

        public LegalDocument? Document { get; set; }

        // Información de Vectorización
        public List<string> ChunkIds { get; set; } = new();
        public int ChunksCreated { get; set; }
        public int TotalCharacters { get; set; }

        // Información de Preguntas Generadas
        public int QuestionsGeneratedCount { get; set; }
        public List<int> SavedQuestionIds { get; set; } = new();
        public List<StudyQuestion> SampleQuestions { get; set; } = new();

        /// <summary>
        /// Distribución de preguntas por nivel de dificultad
        /// </summary>
        public QuestionDifficultyBreakdown? QuestionBreakdown { get; set; }

        // Metadatos de procesamiento
        public int? ProcessingTimeMs { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// Distribución de preguntas generadas por nivel de dificultad
    /// </summary>
    public class QuestionDifficultyBreakdown
    {
        public int Basico { get; set; }
        public int Intermedio { get; set; }
        public int Avanzado { get; set; }

        /// <summary>
        /// Total de preguntas (suma de todos los niveles)
        /// </summary>
        public int Total => Basico + Intermedio + Avanzado;

        /// <summary>
        /// Porcentaje de cada nivel sobre el total
        /// </summary>
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

            // Usar el servicio para crear metadatos
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





    // 🆕 AGREGAR a DocumentController.cs

    /// <summary>
    /// Genera preguntas adicionales para un documento ya subido
    /// </summary>
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

            // 1️⃣ RECUPERAR DOCUMENTO DE QDRANT
            var document = await GetDocumentByIdAsync(documentId);

            if (document == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Documento no encontrado"
                });
            }

            // 2️⃣ GENERAR PREGUNTAS
            var questions = await _questionGeneration.GenerateQuestionsFromDocument(document, count);

            // 3️⃣ GUARDAR EN BD
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

    /// <summary>
    /// Obtiene estadísticas de preguntas por documento
    /// </summary>
    /*
    [HttpGet("{documentId}/questions-stats")]
    public async Task<ActionResult> GetDocumentQuestionsStats(Guid documentId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();

            command.CommandText = @"
        SELECT 
            COUNT(*) as total_questions,
            COUNT(CASE WHEN pg.modalidad_id = 1 THEN 1 END) as written_questions,
            COUNT(CASE WHEN pg.modalidad_id = 2 THEN 1 END) as oral_questions,
            COUNT(CASE WHEN pg.tipo = 'seleccion_multiple' THEN 1 END) as multiple_choice,
            COUNT(CASE WHEN pg.tipo = 'verdadero_falso' THEN 1 END) as true_false,
            COUNT(CASE WHEN pg.nivel = 'basico' THEN 1 END) as basic,
            COUNT(CASE WHEN pg.nivel = 'intermedio' THEN 1 END) as intermediate,
            COUNT(CASE WHEN pg.nivel = 'avanzado' THEN 1 END) as advanced,
            AVG(pg.veces_utilizada) as avg_times_used,
            AVG(pg.tasa_acierto) as avg_success_rate
        FROM preguntas_generadas pg
        INNER JOIN temas t ON pg.tema_id = t.id
        WHERE pg.creada_por LIKE $1
          AND pg.activa = true";

            command.Parameters.Add(new Npgsql.NpgsqlParameter
            {
                Value = $"%{documentId}%"
            });

            using var reader = await command.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                return Ok(new
                {
                    success = true,
                    documentId = documentId,
                    stats = new
                    {
                        totalQuestions = reader.GetInt64(0),
                        writtenQuestions = reader.GetInt64(1),
                        oralQuestions = reader.GetInt64(2),
                        multipleChoice = reader.GetInt64(3),
                        trueFalse = reader.GetInt64(4),
                        byDifficulty = new
                        {
                            basic = reader.GetInt64(5),
                            intermediate = reader.GetInt64(6),
                            advanced = reader.GetInt64(7)
                        },
                        usage = new
                        {
                            avgTimesUsed = reader.IsDBNull(8) ? 0.0 : reader.GetDouble(8),
                            avgSuccessRate = reader.IsDBNull(9) ? 0.0 : (double)reader.GetDecimal(9)
                        }
                    }
                });
            }

            return Ok(new
            {
                success = true,
                documentId = documentId,
                stats = new { totalQuestions = 0 }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas");
            return StatusCode(500, new { success = false, message = ex.Message });
        }
    }


    */


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
    // En DocumentController.cs

    [HttpGet("qdrant/stats")]
    public async Task<ActionResult> ListAllQdrantDocuments()
    {
        try
        {
            // Obtener estadísticas de la colección
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

// DTOs
public class EnhancedDocumentUploadResponse
{
    public bool Success { get; set; }
    public LegalDocument? Document { get; set; }
    public List<string> ChunkIds { get; set; } = new();
    public int ChunksCreated { get; set; }
    public int TotalCharacters { get; set; }
    public List<StudyQuestion> SampleQuestions { get; set; } = new();
    public int ProcessingTimeMs { get; set; }
    public string Message { get; set; } = string.Empty;
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