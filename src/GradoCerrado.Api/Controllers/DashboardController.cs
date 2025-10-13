using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradoCerrado.Domain.Models;

namespace GradoCerrado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly GradocerradoContext _context;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(GradocerradoContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ============================================
    // ESTADÍSTICAS GENERALES
    // ============================================
    [HttpGet("stats/{studentId}")]
    public async Task<ActionResult> GetStudentStats(int studentId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var statsCommand = connection.CreateCommand();

            statsCommand.CommandText = @"
            SELECT 
                COUNT(DISTINCT t.id) as total_tests,
                COUNT(tp.id) as total_questions,
                SUM(CASE WHEN tp.es_correcta = true THEN 1 ELSE 0 END) as correct_answers,
                ROUND(
                    CASE 
                        WHEN COUNT(tp.id) > 0 
                        THEN (SUM(CASE WHEN tp.es_correcta = true THEN 1 ELSE 0 END)::numeric / COUNT(tp.id)::numeric) * 100
                        ELSE 0 
                    END, 2
                ) as success_rate
            FROM tests t
            INNER JOIN test_preguntas tp ON t.id = tp.test_id
            WHERE t.estudiante_id = $1";

            statsCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });

            var stats = new Dictionary<string, object>
            {
                ["totalTests"] = 0,
                ["totalQuestions"] = 0,
                ["correctAnswers"] = 0,
                ["successRate"] = 0.0m,
                ["streak"] = 0
            };

            using (var reader = await statsCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    stats["totalTests"] = reader.IsDBNull(0) ? 0 : reader.GetInt64(0);
                    stats["totalQuestions"] = reader.IsDBNull(1) ? 0 : reader.GetInt64(1);
                    stats["correctAnswers"] = reader.IsDBNull(2) ? 0 : reader.GetInt64(2);
                    stats["successRate"] = reader.IsDBNull(3) ? 0.0m : reader.GetDecimal(3);
                }
            }

            // CALCULAR RACHA
            using var streakCommand = connection.CreateCommand();
            streakCommand.CommandText = @"
            WITH daily_tests AS (
                SELECT DISTINCT DATE(t.fecha_creacion AT TIME ZONE 'UTC' AT TIME ZONE 'America/Santiago') as test_date
                FROM tests t
                INNER JOIN test_preguntas tp ON t.id = tp.test_id
                WHERE t.estudiante_id = $1
                ORDER BY test_date DESC
            ),
            today_chile AS (
                SELECT (CURRENT_TIMESTAMP AT TIME ZONE 'America/Santiago')::date as today
            ),
            consecutive_days AS (
                SELECT 
                    test_date,
                    test_date::date - LAG(test_date::date, 1, test_date::date + 1) OVER (ORDER BY test_date DESC) as gap
                FROM daily_tests, today_chile
                WHERE test_date <= today_chile.today
            )
            SELECT COUNT(*)::int as streak
            FROM consecutive_days
            WHERE test_date >= (
                SELECT COALESCE(
                    (SELECT test_date + INTERVAL '1 day' FROM consecutive_days WHERE gap < -1 ORDER BY test_date DESC LIMIT 1),
                    (SELECT MIN(test_date) FROM daily_tests)
                )
            )
            AND test_date >= (SELECT today - INTERVAL '365 days' FROM today_chile)
            AND EXISTS (SELECT 1 FROM daily_tests, today_chile WHERE test_date >= today_chile.today - INTERVAL '1 day')";

            streakCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });

            try
            {
                var streakResult = await streakCommand.ExecuteScalarAsync();
                stats["streak"] = streakResult != null && streakResult != DBNull.Value ? Convert.ToInt32(streakResult) : 0;
            }
            catch (Exception streakEx)
            {
                _logger.LogWarning(streakEx, "No se pudo calcular la racha, usando 0");
                stats["streak"] = 0;
            }

            return Ok(new { success = true, data = stats });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas");
            return Ok(new { success = true, data = new { totalTests = 0, totalQuestions = 0, correctAnswers = 0, successRate = 0.0m, streak = 0 } });
        }
    }

    // ============================================
    // SESIONES RECIENTES
    // ============================================
    [HttpGet("recent-sessions/{studentId}")]
    public async Task<ActionResult> GetRecentSessions(int studentId, [FromQuery] int limit = 10)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();

            command.CommandText = @"
                SELECT 
                    t.id,
                    t.fecha_creacion as date,
                    COALESCE(a.nombre, 'General') as area,
                    COALESCE(EXTRACT(EPOCH FROM (t.hora_fin - t.hora_inicio))::int, 0) as duration_seconds,
                    COUNT(tp.id) as total_questions,
                    SUM(CASE WHEN tp.es_correcta = true THEN 1 ELSE 0 END) as correct_answers,
                    ROUND(
                        CASE 
                            WHEN COUNT(tp.id) > 0 
                            THEN (SUM(CASE WHEN tp.es_correcta = true THEN 1 ELSE 0 END)::numeric / COUNT(tp.id)::numeric) * 100
                            ELSE 0 
                        END, 2
                    ) as success_rate
                FROM tests t
                LEFT JOIN areas a ON t.area_id = a.id
                INNER JOIN test_preguntas tp ON t.id = tp.test_id
                WHERE t.estudiante_id = $1
                GROUP BY t.id, t.fecha_creacion, a.nombre, t.hora_inicio, t.hora_fin
                HAVING COUNT(tp.id) > 0
                ORDER BY t.fecha_creacion DESC
                LIMIT $2";

            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });
            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = limit });

            var sessions = new List<object>();

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var durationSeconds = reader.IsDBNull(3) ? 0 : reader.GetInt32(3);
                    var minutes = Math.Max(1, durationSeconds / 60);

                    sessions.Add(new
                    {
                        id = reader.GetInt32(0),
                        date = reader.GetDateTime(1),
                        area = reader.GetString(2),
                        duration = $"{minutes} min",
                        questions = reader.GetInt64(4),
                        correct = reader.IsDBNull(5) ? 0 : reader.GetInt64(5),
                        successRate = reader.IsDBNull(6) ? 0.0m : reader.GetDecimal(6)
                    });
                }
            }

            return Ok(new { success = true, data = sessions });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo sesiones recientes");
            return Ok(new { success = true, data = new List<object>() });
        }
    }

    // ============================================
    // GRÁFICO SEMANAL
    // ============================================
    [HttpGet("weekly-progress/{studentId}")]
    public async Task<ActionResult> GetWeeklyProgress(int studentId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();

            command.CommandText = @"
            WITH chile_dates AS (
                SELECT 
                    DATE(t.fecha_creacion AT TIME ZONE 'UTC' AT TIME ZONE 'America/Santiago') as test_date,
                    EXTRACT(DOW FROM (t.fecha_creacion AT TIME ZONE 'UTC' AT TIME ZONE 'America/Santiago')) as day_of_week,
                    COUNT(tp.id) as question_count,
                    COALESCE(a.nombre, 'General') as area
                FROM tests t
                LEFT JOIN areas a ON t.area_id = a.id
                INNER JOIN test_preguntas tp ON t.id = tp.test_id
                WHERE t.estudiante_id = $1
                  AND DATE(t.fecha_creacion AT TIME ZONE 'UTC' AT TIME ZONE 'America/Santiago') >= 
                      (CURRENT_TIMESTAMP AT TIME ZONE 'America/Santiago')::date - INTERVAL '6 days'
                GROUP BY test_date, day_of_week, a.nombre
            )
            SELECT 
                day_of_week,
                area,
                SUM(question_count) as total_questions
            FROM chile_dates
            GROUP BY day_of_week, area
            ORDER BY day_of_week";

            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });

            var weeklyData = new Dictionary<int, Dictionary<string, int>>();

            for (int i = 0; i <= 6; i++)
            {
                weeklyData[i] = new Dictionary<string, int> { ["Civil"] = 0, ["Procesal"] = 0 };
            }

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    int dayOfWeek = Convert.ToInt32(reader.GetDouble(0));
                    string area = reader.GetString(1);
                    int count = Convert.ToInt32(reader.GetInt64(2));

                    if (weeklyData.ContainsKey(dayOfWeek))
                    {
                        if (area.Contains("Civil") || area == "General")
                            weeklyData[dayOfWeek]["Civil"] += count;
                        else if (area.Contains("Procesal"))
                            weeklyData[dayOfWeek]["Procesal"] += count;
                    }
                }
            }

            var dayNames = new[] { "Dom", "Lun", "Mar", "Mié", "Jue", "Vie", "Sáb" };
            var result = new List<object>();

            for (int i = 1; i <= 6; i++)
            {
                result.Add(new
                {
                    date = dayNames[i],
                    civil = weeklyData[i]["Civil"],
                    procesal = weeklyData[i]["Procesal"],
                    total = weeklyData[i]["Civil"] + weeklyData[i]["Procesal"]
                });
            }
            result.Add(new
            {
                date = dayNames[0],
                civil = weeklyData[0]["Civil"],
                procesal = weeklyData[0]["Procesal"],
                total = weeklyData[0]["Civil"] + weeklyData[0]["Procesal"]
            });

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo progreso semanal");
            return Ok(new { success = true, data = new List<object>() });
        }
    }

    // ============================================
    // 🆕 ESTADÍSTICAS JERÁRQUICAS (ÁREA → TEMAS → SUBTEMAS)
    // ============================================
    [HttpGet("hierarchical-stats/{studentId}")]
    public async Task<ActionResult> GetHierarchicalStats(int studentId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            // 1. General
            using var generalCommand = connection.CreateCommand();
            generalCommand.CommandText = @"
                SELECT 
                    COUNT(DISTINCT t.id) as total_sessions,
                    COUNT(tp.id) as total_questions,
                    SUM(CASE WHEN tp.es_correcta = true THEN 1 ELSE 0 END) as correct_answers,
                    ROUND(
                        CASE 
                            WHEN COUNT(tp.id) > 0 
                            THEN (SUM(CASE WHEN tp.es_correcta = true THEN 1 ELSE 0 END)::numeric / COUNT(tp.id)::numeric) * 100
                            ELSE 0 
                        END, 2
                    ) as success_rate
                FROM tests t
                INNER JOIN test_preguntas tp ON t.id = tp.test_id
                WHERE t.estudiante_id = $1";
            
            generalCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });

            var result = new List<object>();

            using (var reader = await generalCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    result.Add(new
                    {
                        type = "general",
                        area = "General",
                        sessions = reader.GetInt64(0),
                        totalQuestions = reader.GetInt64(1),
                        correctAnswers = reader.IsDBNull(2) ? 0 : reader.GetInt64(2),
                        successRate = reader.IsDBNull(3) ? 0.0m : reader.GetDecimal(3)
                    });
                }
            }

            // 2. Área → Temas → Subtemas
            using var hierarchyCommand = connection.CreateCommand();
            hierarchyCommand.CommandText = @"
                SELECT 
                    a.id as area_id,
                    a.nombre as area_nombre,
                    t.id as tema_id,
                    t.nombre as tema_nombre,
                    s.id as subtema_id,
                    s.nombre as subtema_nombre,
                    COUNT(tp.id) as preguntas_respondidas,
                    SUM(CASE WHEN tp.es_correcta = true THEN 1 ELSE 0 END) as preguntas_correctas,
                    ROUND(
                        CASE 
                            WHEN COUNT(tp.id) > 0 
                            THEN (SUM(CASE WHEN tp.es_correcta = true THEN 1 ELSE 0 END)::numeric / COUNT(tp.id)::numeric) * 100
                            ELSE 0 
                        END, 2
                    ) as porcentaje_acierto
                FROM areas a
                INNER JOIN temas t ON t.area_id = a.id
                LEFT JOIN subtemas s ON s.tema_id = t.id
                LEFT JOIN preguntas_generadas pg ON pg.subtema_id = s.id OR (s.id IS NULL AND pg.tema_id = t.id)
                LEFT JOIN test_preguntas tp ON tp.pregunta_generada_id = pg.id
                LEFT JOIN tests test ON test.id = tp.test_id AND test.estudiante_id = $1
                WHERE a.id = 1 AND t.activo = true AND (s.activo = true OR s.id IS NULL)
                GROUP BY a.id, a.nombre, t.id, t.nombre, s.id, s.nombre
                ORDER BY t.nombre, s.nombre";

            hierarchyCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });

            var temasByArea = new Dictionary<int, List<object>>();
            var subtemasByTema = new Dictionary<int, List<object>>();

            using (var reader = await hierarchyCommand.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    int temaId = reader.GetInt32(2);
                    string temaNombre = reader.GetString(3);
                    int? subtemaId = reader.IsDBNull(4) ? null : reader.GetInt32(4);
                    string? subtemaNombre = reader.IsDBNull(5) ? null : reader.GetString(5);
                    long preguntasRespondidas = reader.GetInt64(6);
                    long preguntasCorrectas = reader.IsDBNull(7) ? 0 : reader.GetInt64(7);
                    decimal porcentaje = reader.IsDBNull(8) ? 0.0m : reader.GetDecimal(8);

                    if (subtemaId.HasValue && !string.IsNullOrEmpty(subtemaNombre))
                    {
                        if (!subtemasByTema.ContainsKey(temaId))
                            subtemasByTema[temaId] = new List<object>();

                        subtemasByTema[temaId].Add(new
                        {
                            subtemaId = subtemaId.Value,
                            subtemaNombre = subtemaNombre,
                            totalPreguntas = preguntasRespondidas,
                            preguntasCorrectas = preguntasCorrectas,
                            porcentajeAcierto = porcentaje
                        });
                    }
                }
            }

            // 3. Construir temas con subtemas
            temasByArea[1] = new List<object>();

            foreach (var temaId in subtemasByTema.Keys)
            {
                var subtemas = subtemasByTema[temaId];
                long totalPreguntas = subtemas.Sum(s => (long)((dynamic)s).totalPreguntas);
                long preguntasCorrectas = subtemas.Sum(s => (long)((dynamic)s).preguntasCorrectas);
                decimal porcentajeTema = totalPreguntas > 0 ? Math.Round((decimal)preguntasCorrectas / totalPreguntas * 100, 2) : 0;

                using var temaCommand = connection.CreateCommand();
                temaCommand.CommandText = "SELECT nombre FROM temas WHERE id = $1";
                temaCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = temaId });
                string temaNombre = (string)(await temaCommand.ExecuteScalarAsync() ?? "");

                temasByArea[1].Add(new
                {
                    type = "tema",
                    temaId = temaId,
                    temaNombre = temaNombre,
                    totalPreguntas = totalPreguntas,
                    preguntasCorrectas = preguntasCorrectas,
                    porcentajeAcierto = porcentajeTema,
                    subtemas = subtemas
                });
            }

            result.Add(new
            {
                type = "area",
                area = "Derecho Civil",
                temas = temasByArea[1]
            });

            return Ok(new { success = true, data = result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas jerárquicas");
            return Ok(new { success = true, data = new List<object>() });
        }
    }
}