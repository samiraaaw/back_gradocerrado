using Microsoft.AspNetCore.Mvc;
using GradoCerrado.Application.Interfaces;
using GradoCerrado.Domain.Entities;
using GradoCerrado.Infrastructure.DTOs;
using GradoCerrado.Infrastructure.Services;
using GradoCerrado.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Extensions.Logging;

namespace GradoCerrado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudyController : ControllerBase
{
    private readonly ILogger<StudyController> _logger;
    private readonly IAIService _aiService;
    private readonly IVectorService _vectorService;
    private readonly IQuestionPersistenceService _questionPersistence;
    private readonly GradocerradoContext _context;

    public StudyController(
        ILogger<StudyController> logger,
        IAIService aiService,
        IVectorService vectorService,
        IQuestionPersistenceService questionPersistence,
        GradocerradoContext context)
    {
        _logger = logger;
        _aiService = aiService;
        _vectorService = vectorService;
        _questionPersistence = questionPersistence;
        _context = context;
    }

    [HttpGet("registered-users")]
    public ActionResult GetRegisteredUsers()
    {
        try
        {
            var users = new[]
            {
                new
                {
                    id = "2a5f109f-37da-41a6-91f1-d8df4b7ba02a",
                    name = "Coni",
                    email = "coni@gmail.com",
                    createdAt = "2025-09-25T03:00:10.427677Z"
                },
                new
                {
                    id = "9971d353-41e7-4a5c-a7c5-a6f620386ed5",
                    name = "alumno1",
                    email = "alumno1@gmail.com",
                    createdAt = "2025-09-24T23:26:28.101947Z"
                }
            };

            return Ok(new
            {
                success = true,
                totalUsers = users.Length,
                users
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo usuarios registrados");
            return StatusCode(500, new { success = false, message = "Error consultando usuarios" });
        }
    }

    [HttpPost("login")]
    public ActionResult Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                return BadRequest(new { success = false, message = "Email es obligatorio" });

            var user = new
            {
                id = Guid.NewGuid(),
                name = "Usuario de prueba",
                email = request.Email.Trim()
            };

            return Ok(new
            {
                success = true,
                message = "Login exitoso",
                user
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en login: {Email}", request.Email);
            return StatusCode(500, new { success = false, message = "Error interno del servidor" });
        }
    }

    // ✅ ACTUALIZADO: StartSession con modo adaptativo
    [HttpPost("start-session")]
    public async Task<ActionResult> StartSession([FromBody] StudySessionRequest request)
    {
        try
        {
            if (request.StudentId <= 0)
            {
                return BadRequest(new { success = false, message = "StudentId es obligatorio" });
            }

            _logger.LogInformation("📚 Iniciando sesión para estudiante ID: {StudentId}", request.StudentId);
            _logger.LogInformation("🎯 Modo adaptativo: {AdaptiveMode}", request.AdaptiveMode ?? false);

            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            // 1️⃣ VALIDAR ESTUDIANTE
            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Id == request.StudentId && e.Activo == true);

            if (estudiante == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Estudiante con ID {request.StudentId} no encontrado"
                });
            }

            _logger.LogInformation("✅ Estudiante encontrado: {Id} - {Nombre}", estudiante.Id, estudiante.Nombre);

            // 2️⃣ CREAR TEST
            var utcNow = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
            int testId;

            using (var testCommand = connection.CreateCommand())
            {
                // Intentar con modo_adaptativo primero
                try
                {
                    testCommand.CommandText = @"
                        INSERT INTO tests 
                        (estudiante_id, tipo_test_id, numero_preguntas_total, 
                         hora_inicio, completado, fecha_creacion, modo_adaptativo)
                        VALUES 
                        ($1, $2, $3, $4, $5, $6, $7)
                        RETURNING id";

                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = estudiante.Id });
                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 1 });
                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = request.QuestionCount ?? 5 });
                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = utcNow });
                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = false });
                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = utcNow });
                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = request.AdaptiveMode ?? false });

                    testId = Convert.ToInt32(await testCommand.ExecuteScalarAsync());
                }
                catch (Exception ex)
                {
                    // Si falla, usar sin modo_adaptativo
                    _logger.LogWarning("⚠️ Columna modo_adaptativo no existe: {Message}", ex.Message);

                    testCommand.Parameters.Clear();
                    testCommand.CommandText = @"
                        INSERT INTO tests 
                        (estudiante_id, tipo_test_id, numero_preguntas_total, 
                         hora_inicio, completado, fecha_creacion)
                        VALUES 
                        ($1, $2, $3, $4, $5, $6)
                        RETURNING id";

                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = estudiante.Id });
                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 1 });
                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = request.QuestionCount ?? 5 });
                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = utcNow });
                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = false });
                    testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = utcNow });

                    testId = Convert.ToInt32(await testCommand.ExecuteScalarAsync());
                }
            }

            _logger.LogInformation("🆕 Test creado con ID: {TestId}", testId);

            // 3️⃣ RECUPERAR PREGUNTAS DE BASE DE DATOS
            var questions = await GetQuestionsFromDatabase(
                legalAreas: request.LegalAreas,
                difficulty: request.Difficulty,
                count: request.QuestionCount ?? 5,
                temaId: request.TemaId,
                subtemaId: request.SubtemaId,
                connection: connection
            );

            if (!questions.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No hay preguntas disponibles para las áreas seleccionadas. Sube documentos primero."
                });
            }

            _logger.LogInformation("✅ {Count} preguntas recuperadas de BD", questions.Count);

            // 4️⃣ FORMATEAR RESPUESTA
            return Ok(new
            {
                success = true,
                testId = testId,
                session = new
                {
                    sessionId = Guid.NewGuid(),
                    studentId = request.StudentId,
                    realStudentId = estudiante.Id,
                    startTime = DateTime.UtcNow,
                    difficulty = request.Difficulty,
                    legalAreas = request.LegalAreas,
                    adaptiveMode = request.AdaptiveMode ?? false,
                    status = "Active"
                },
                questions = questions,
                totalQuestions = questions.Count,
                generatedWithAI = false,
                source = "database",
                adaptiveEnabled = request.AdaptiveMode ?? false,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error iniciando sesión");
            return StatusCode(500, new
            {
                success = false,
                message = "Error interno del servidor",
                error = ex.Message
            });
        }
    }

    private async Task<List<object>> GetQuestionsFromDatabase(
    List<string> legalAreas,
    string difficulty,
    int count,
    int? temaId,
    int? subtemaId,
    System.Data.Common.DbConnection connection)
{
    var questions = new List<object>();

    using var command = connection.CreateCommand();

    // Construir WHERE dinámico
    var whereClauses = new List<string>
    {
        "pg.activa = true",
        "pg.nivel = $1::nivel_dificultad"
    };

    int paramIndex = 2;

    // Filtro por área
    if (legalAreas != null && legalAreas.Any())
    {
        whereClauses.Add($"a.nombre = ANY(${paramIndex}::text[])");
        paramIndex++;
    }

    // Filtro por subtema (prioridad sobre tema)
    if (subtemaId.HasValue)
    {
        whereClauses.Add($"pg.subtema_id = ${paramIndex}");
        paramIndex++;
    }
    // Filtro por tema (solo si no hay subtema)
    else if (temaId.HasValue)
    {
        whereClauses.Add($"t.id = ${paramIndex}");
        paramIndex++;
    }

    var whereClause = string.Join(" AND ", whereClauses);

    command.CommandText = $@"
        WITH preguntas_unicas AS (
            SELECT 
                pg.id,
                pg.texto_pregunta,
                pg.tipo,
                pg.nivel,
                t.nombre as tema,
                pg.respuesta_correcta_boolean,
                pg.respuesta_correcta_opcion,
                pg.explicacion
            FROM preguntas_generadas pg
            INNER JOIN temas t ON pg.tema_id = t.id
            INNER JOIN areas a ON t.area_id = a.id
            WHERE {whereClause}
            ORDER BY RANDOM()
            LIMIT ${paramIndex}
        )
        SELECT 
            pu.id,
            pu.texto_pregunta,
            pu.tipo,
            pu.nivel,
            pu.tema,
            pu.respuesta_correcta_boolean,
            pu.respuesta_correcta_opcion,
            pu.explicacion,
            po.opcion,
            po.texto_opcion,
            po.es_correcta
        FROM preguntas_unicas pu
        LEFT JOIN pregunta_opciones po ON pu.id = po.pregunta_generada_id
        ORDER BY pu.id, po.opcion";

    // Agregar parámetros en orden
    command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = difficulty.ToLower() });

    if (legalAreas != null && legalAreas.Any())
    {
        command.Parameters.Add(new Npgsql.NpgsqlParameter
        {
            Value = legalAreas.ToArray(),
            NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text
        });
    }

    if (subtemaId.HasValue)
    {
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = subtemaId.Value });
    }
    else if (temaId.HasValue)
    {
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = temaId.Value });
    }

    command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = count });

    using var reader = await command.ExecuteReaderAsync();

    var preguntasDict = new Dictionary<int, PreguntaConOpciones>();

    while (await reader.ReadAsync())
    {
        var preguntaId = reader.GetInt32(0);

        if (!preguntasDict.ContainsKey(preguntaId))
        {
            preguntasDict[preguntaId] = new PreguntaConOpciones
            {
                Id = preguntaId,
                TextoPregunta = reader.GetString(1),
                Tipo = reader.GetString(2),
                Nivel = reader.GetString(3),
                Tema = reader.GetString(4),
                RespuestaBoolean = reader.IsDBNull(5) ? (bool?)null : reader.GetBoolean(5),
                RespuestaOpcion = reader.IsDBNull(6) ? (char?)null : reader.GetChar(6),
                Explicacion = reader.IsDBNull(7) ? "" : reader.GetString(7),
                Opciones = new List<OpcionDTO>()
            };
        }

        if (!reader.IsDBNull(8))
        {
            preguntasDict[preguntaId].Opciones.Add(new OpcionDTO
            {
                Id = reader.GetChar(8).ToString(),
                Text = reader.GetString(9),
                IsCorrect = reader.GetBoolean(10)
            });
        }
    }

    foreach (var pregunta in preguntasDict.Values.Take(count))
    {
        var questionObj = new
        {
            id = pregunta.Id,
            questionText = pregunta.TextoPregunta,
            type = pregunta.Tipo,
            level = pregunta.Nivel,
            tema = pregunta.Tema,
            options = pregunta.Tipo == "seleccion_multiple" 
                ? pregunta.Opciones.ToArray() 
                : pregunta.Tipo == "verdadero_falso"
                    ? new[]
                    {
                        new OpcionDTO { Id = "A", Text = "Verdadero", IsCorrect = pregunta.RespuestaBoolean == true },
                        new OpcionDTO { Id = "B", Text = "Falso", IsCorrect = pregunta.RespuestaBoolean == false }
                    }
                    : null,
            correctAnswer = pregunta.Tipo == "seleccion_multiple"
                ? pregunta.RespuestaOpcion?.ToString()
                : pregunta.RespuestaBoolean?.ToString().ToLower(),
            explanation = pregunta.Explicacion
        };

        questions.Add(questionObj);
    }

    return questions;
}

    [HttpPost("submit-answer")]
    public async Task<ActionResult> SubmitAnswer([FromBody] SubmitAnswerRequest request)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            bool esCorrecta = request.IsCorrect;
            char? respuestaOpcion = null;
            bool? respuestaBoolean = null;

            // Detectar si es V/F o selección múltiple
            if (request.UserAnswer?.ToLower() == "true" || request.UserAnswer?.ToLower() == "false")
            {
                respuestaBoolean = bool.Parse(request.UserAnswer);
                _logger.LogInformation("Respuesta V/F detectada: {Answer}", respuestaBoolean);
            }
            else
            {
                respuestaOpcion = ExtractAnswerLetter(request.UserAnswer);
                if (!respuestaOpcion.HasValue)
                {
                    _logger.LogWarning("No se pudo extraer letra de respuesta de: {UserAnswer}", request.UserAnswer);
                    return BadRequest(new { success = false, message = "Formato de respuesta inválido" });
                }
            }

            int timeSpentSeconds = ParseTimeSpanToSeconds(request.TimeSpent);
            var utcNow = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            using var command = connection.CreateCommand();

            // Intentar con respuesta_boolean primero
            try
            {
                command.CommandText = @"
                    INSERT INTO test_preguntas 
                    (test_id, pregunta_generada_id, respuesta_opcion, respuesta_boolean, es_correcta, 
                     tiempo_respuesta_segundos, fecha_respuesta, numero_orden)
                    VALUES 
                    ($1, $2, $3, $4, $5, $6, $7, $8)
                    RETURNING id";

                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = request.TestId });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = request.PreguntaId });
                command.Parameters.Add(new Npgsql.NpgsqlParameter
                {
                    Value = respuestaOpcion.HasValue ? (object)respuestaOpcion.Value : DBNull.Value
                });
                command.Parameters.Add(new Npgsql.NpgsqlParameter
                {
                    Value = respuestaBoolean.HasValue ? (object)respuestaBoolean.Value : DBNull.Value
                });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = esCorrecta });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = timeSpentSeconds });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = utcNow });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = request.NumeroOrden });
            }
            catch (Exception ex)
            {
                // Si falla, usar solo respuesta_opcion
                _logger.LogWarning("⚠️ Columna respuesta_boolean no existe: {Message}", ex.Message);

                command.Parameters.Clear();
                command.CommandText = @"
                    INSERT INTO test_preguntas 
                    (test_id, pregunta_generada_id, respuesta_opcion, es_correcta, 
                     tiempo_respuesta_segundos, fecha_respuesta, numero_orden)
                    VALUES 
                    ($1, $2, $3, $4, $5, $6, $7)
                    RETURNING id";

                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = request.TestId });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = request.PreguntaId });
                command.Parameters.Add(new Npgsql.NpgsqlParameter
                {
                    Value = respuestaOpcion.HasValue ? (object)respuestaOpcion.Value :
                            (respuestaBoolean.HasValue ? (object)(respuestaBoolean.Value ? 'V' : 'F') : DBNull.Value)
                });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = esCorrecta });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = timeSpentSeconds });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = utcNow });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = request.NumeroOrden });
            }

            var respuestaId = Convert.ToInt32(await command.ExecuteScalarAsync());

            _logger.LogInformation(
                "Respuesta guardada - TestId: {TestId}, PreguntaId: {PreguntaId}, Opción: {Opcion}, Boolean: {Boolean}, Correcta: {Correcta}",
                request.TestId, request.PreguntaId, respuestaOpcion?.ToString() ?? "null",
                respuestaBoolean?.ToString() ?? "null", esCorrecta);

            return Ok(new
            {
                success = true,
                isCorrect = esCorrecta,
                respuestaId,
                explanation = request.Explanation,
                correctAnswer = request.CorrectAnswer
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error guardando respuesta");
            return StatusCode(500, new
            {
                success = false,
                message = "Error guardando respuesta",
                error = ex.Message
            });
        }
    }

    // ✅ NUEVO: Endpoint para obtener temas débiles
    [HttpGet("weak-topics/{studentId}")]
    public async Task<ActionResult> GetWeakTopics(int studentId)
    {
        try
        {
            _logger.LogInformation("📊 Obteniendo temas débiles para estudiante {StudentId}", studentId);

            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                WITH respuestas_por_tema AS (
                    SELECT 
                        t.nombre as tema,
                        t.id as tema_id,
                        COUNT(*) as total_intentos,
                        SUM(CASE WHEN tp.es_correcta = false THEN 1 ELSE 0 END) as total_errores,
                        SUM(CASE WHEN tp.es_correcta = true THEN 1 ELSE 0 END) as total_aciertos
                    FROM test_preguntas tp
                    INNER JOIN tests ts ON tp.test_id = ts.id
                    INNER JOIN preguntas_generadas pg ON tp.pregunta_generada_id = pg.id
                    INNER JOIN temas t ON pg.tema_id = t.id
                    WHERE ts.estudiante_id = $1
                      AND ts.completado = true
                    GROUP BY t.nombre, t.id
                    HAVING COUNT(*) >= 3
                )
                SELECT 
                    tema,
                    tema_id,
                    total_intentos,
                    total_errores,
                    total_aciertos,
                    ROUND(CAST(total_errores AS DECIMAL) / total_intentos * 100, 1) as tasa_error
                FROM respuestas_por_tema
                WHERE CAST(total_errores AS DECIMAL) / total_intentos > 0.3
                ORDER BY tasa_error DESC
                LIMIT 10";

            command.Parameters.Add(new NpgsqlParameter { Value = studentId });

            var weakTopics = new List<object>();

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                weakTopics.Add(new
                {
                    nombre = reader.GetString(0),
                    temaId = reader.GetInt32(1),
                    totalIntentos = reader.GetInt32(2),
                    totalErrores = reader.GetInt32(3),
                    totalAciertos = reader.GetInt32(4),
                    tasaError = reader.GetDouble(5)
                });
            }

            _logger.LogInformation("✅ {Count} temas débiles encontrados", weakTopics.Count);

            return Ok(new
            {
                success = true,
                data = weakTopics,
                totalWeakTopics = weakTopics.Count,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo temas débiles");
            return StatusCode(500, new
            {
                success = false,
                message = "Error obteniendo temas débiles",
                error = ex.Message
            });
        }
    }

    // ✅ NUEVO: Obtener configuración de modo adaptativo
    [HttpGet("adaptive-mode/{studentId}")]
    public async Task<ActionResult> GetAdaptiveMode(int studentId)
    {
        try
        {
            _logger.LogInformation("📊 Obteniendo configuración adaptativa para estudiante {StudentId}", studentId);

            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Id == studentId && e.Activo == true);

            if (estudiante == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Estudiante con ID {studentId} no encontrado"
                });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    studentId = estudiante.Id,
                    adaptiveModeEnabled = estudiante.ModoAdaptativoActivo
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error obteniendo configuración adaptativa");
            return StatusCode(500, new
            {
                success = false,
                message = "Error obteniendo configuración",
                error = ex.Message
            });
        }
    }

    // ✅ NUEVO: Actualizar configuración de modo adaptativo
    [HttpPut("adaptive-mode/{studentId}")]
    public async Task<ActionResult> UpdateAdaptiveMode(int studentId, [FromBody] AdaptiveModeRequest request)
    {
        try
        {
            _logger.LogInformation("💾 Actualizando modo adaptativo para estudiante {StudentId}: {Enabled}",
                studentId, request.Enabled);

            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Id == studentId && e.Activo == true);

            if (estudiante == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = $"Estudiante con ID {studentId} no encontrado"
                });
            }

            estudiante.ModoAdaptativoActivo = request.Enabled;
            estudiante.FechaModificacion = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("✅ Modo adaptativo actualizado correctamente");

            return Ok(new
            {
                success = true,
                message = request.Enabled ? "Modo adaptativo activado" : "Modo adaptativo desactivado",
                data = new
                {
                    studentId = estudiante.Id,
                    adaptiveModeEnabled = estudiante.ModoAdaptativoActivo
                },
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error actualizando modo adaptativo");
            return StatusCode(500, new
            {
                success = false,
                message = "Error actualizando configuración",
                error = ex.Message
            });
        }
    }

    [HttpPost("start-oral-session")]
    public async Task<ActionResult> StartOralSession([FromBody] StudySessionRequest request)
    {
        try
        {
            if (request.StudentId <= 0)
            {
                return BadRequest(new { success = false, message = "StudentId es obligatorio" });
            }

            _logger.LogInformation("🎤 Iniciando sesión ORAL para estudiante ID: {StudentId}", request.StudentId);

            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Id == request.StudentId && e.Activo == true);

            if (estudiante == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Estudiante con ID {request.StudentId} no encontrado"
                });
            }

            using var testCommand = connection.CreateCommand();
            testCommand.CommandText = @"
                INSERT INTO tests 
                (estudiante_id, modalidad_id, tipo_test_id, numero_preguntas_total, 
                 hora_inicio, completado, fecha_creacion)
                VALUES 
                ($1, $2, $3, $4, $5, $6, $7)
                RETURNING id";

            var utcNow = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

            testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = estudiante.Id });
            testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 2 });
            testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 1 });
            testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = request.QuestionCount ?? 5 });
            testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = utcNow });
            testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = false });
            testCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = utcNow });

            var testId = Convert.ToInt32(await testCommand.ExecuteScalarAsync());

            _logger.LogInformation("✅ Test ORAL creado con ID: {TestId}", testId);

            var questions = await GetOralQuestionsFromDatabase(
                legalAreas: request.LegalAreas,
                difficulty: request.Difficulty,
                count: request.QuestionCount ?? 5,
                connection: connection
            );

            if (!questions.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No hay preguntas orales disponibles. Sube documentos con modo oral primero."
                });
            }

            _logger.LogInformation("✅ {Count} preguntas ORALES recuperadas", questions.Count);

            return Ok(new
            {
                success = true,
                testId = testId,
                session = new
                {
                    sessionId = Guid.NewGuid(),
                    studentId = request.StudentId,
                    realStudentId = estudiante.Id,
                    startTime = DateTime.UtcNow,
                    difficulty = request.Difficulty,
                    legalAreas = request.LegalAreas,
                    mode = "ORAL",
                    status = "Active"
                },
                questions = questions,
                totalQuestions = questions.Count,
                generatedWithAI = false,
                evaluationMode = "AI-Powered",
                source = "database",
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generando sesión ORAL");
            return StatusCode(500, new
            {
                success = false,
                message = "Error interno del servidor",
                error = ex.Message
            });
        }
    }

    private async Task<List<object>> GetOralQuestionsFromDatabase(
        List<string> legalAreas,
        string difficulty,
        int count,
        System.Data.Common.DbConnection connection)
    {
        var questions = new List<object>();

        using var command = connection.CreateCommand();

        command.CommandText = @"
            SELECT 
                pg.id,
                pg.texto_pregunta,
                pg.tipo,
                pg.nivel,
                t.nombre as tema,
                pg.respuesta_modelo,
                pg.explicacion
            FROM preguntas_generadas pg
            INNER JOIN temas t ON pg.tema_id = t.id
            INNER JOIN areas a ON t.area_id = a.id
            WHERE pg.activa = true
              AND pg.modalidad_id = 2
              AND pg.nivel = $1::nivel_dificultad
              AND (
                  $2::text[] IS NULL 
                  OR a.nombre = ANY($2::text[])
              )
            ORDER BY RANDOM()
            LIMIT $3";

        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = difficulty.ToLower() });
        command.Parameters.Add(new Npgsql.NpgsqlParameter
        {
            Value = legalAreas.Any() ? legalAreas.ToArray() : (object)DBNull.Value,
            NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text
        });
        command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = count });

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            questions.Add(new
            {
                id = reader.GetInt32(0),
                questionText = reader.GetString(1),
                type = "oral",
                level = reader.GetString(3),
                tema = reader.GetString(4),
                expectedAnswer = reader.IsDBNull(5) ? "" : reader.GetString(5),
                explanation = reader.IsDBNull(6) ? "" : reader.GetString(6),
                evaluationCriteria = new
                {
                    allowPartialCredit = true,
                    flexibility = "high"
                }
            });
        }

        return questions;
    }

    private char? ExtractAnswerLetter(string? userAnswer)
    {
        if (string.IsNullOrWhiteSpace(userAnswer))
            return null;

        if (userAnswer.Length == 1 && char.IsLetter(userAnswer[0]))
        {
            return char.ToUpper(userAnswer[0]);
        }

        var validLetters = new[] { 'A', 'B', 'C', 'D' };
        var foundLetter = userAnswer.FirstOrDefault(c => validLetters.Contains(char.ToUpper(c)));

        if (foundLetter != default(char))
        {
            return char.ToUpper(foundLetter);
        }

        return char.ToUpper(userAnswer[0]);
    }

    private int ParseTimeSpanToSeconds(string? timeSpanString)
    {
        if (string.IsNullOrWhiteSpace(timeSpanString))
            return 0;

        try
        {
            var duration = System.Xml.XmlConvert.ToTimeSpan(timeSpanString);
            return (int)duration.TotalSeconds;
        }
        catch
        {
            var numbers = System.Text.RegularExpressions.Regex.Matches(timeSpanString, @"\d+");
            if (numbers.Count > 0)
            {
                return int.Parse(numbers[numbers.Count - 1].Value);
            }
            return 0;
        }
    }

    private class PreguntaConOpciones
    {
        public int Id { get; set; }
        public string TextoPregunta { get; set; } = "";
        public string Tipo { get; set; } = "";
        public string Nivel { get; set; } = "";
        public string Tema { get; set; } = "";
        public bool? RespuestaBoolean { get; set; }
        public char? RespuestaOpcion { get; set; }
        public string Explicacion { get; set; } = "";
        public List<OpcionDTO> Opciones { get; set; } = new();
    }

    private class OpcionDTO
    {
        public string Id { get; set; } = "";
        public string Text { get; set; } = "";
        public bool IsCorrect { get; set; }
    }


    public class StudySessionRequest
    {
        public int StudentId { get; set; }
        public string Difficulty { get; set; } = "basico";
        public List<string> LegalAreas { get; set; } = new();
        public int? QuestionCount { get; set; } = 5;
        public int? TemaId { get; set; } = null;
        public int? SubtemaId { get; set; } = null;

        // ✅ AGREGAR ESTA LÍNEA
        public bool? AdaptiveMode { get; set; } = false;
    }

    public class RegisterStudentRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = string.Empty;
    }

    public class SubmitAnswerRequest
    {
        public int TestId { get; set; }
        public int PreguntaId { get; set; }
        public string? UserAnswer { get; set; }
        public string? CorrectAnswer { get; set; }
        public string? Explanation { get; set; }
        public string? TimeSpent { get; set; }
        public int NumeroOrden { get; set; } = 1;
        public bool IsCorrect { get; set; }
    }

    public class AdaptiveModeRequest
    {
        public bool Enabled { get; set; }
    }
}