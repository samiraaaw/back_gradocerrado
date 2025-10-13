// src/GradoCerrado.Api/Controllers/WeaknessController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradoCerrado.Domain.Models;

namespace GradoCerrado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class WeaknessController : ControllerBase
{
    private readonly GradocerradoContext _context;
    private readonly ILogger<WeaknessController> _logger;

    public WeaknessController(GradocerradoContext context, ILogger<WeaknessController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════
    // 📊 GET: Debilidades por Tema
    // ═══════════════════════════════════════════════════════
    [HttpGet("temas/{studentId}")]
    public async Task<ActionResult> GetDebilidadesTemas(int studentId, [FromQuery] int limit = 10)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    area, tema, total_intentos, tasa_acierto, 
                    nivel_dominio, dias_sin_practicar,
                    racha_aciertos_consecutivos, tiempo_promedio_respuesta
                FROM vista_debilidades_temas
                WHERE estudiante_id = $1
                  AND nivel_dominio IN ('critico', 'debil', 'medio')
                ORDER BY 
                    CASE nivel_dominio
                        WHEN 'critico' THEN 1
                        WHEN 'debil' THEN 2
                        WHEN 'medio' THEN 3
                    END,
                    tasa_acierto ASC
                LIMIT $2";

            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });
            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = limit });

            var debilidades = new List<object>();

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    debilidades.Add(new
                    {
                        area = reader.GetString(0),
                        tema = reader.GetString(1),
                        intentos = reader.GetInt32(2),
                        tasaAcierto = reader.GetDecimal(3),
                        nivelDominio = reader.GetString(4),
                        diasSinPracticar = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                        racha = reader.GetInt32(6),
                        tiempoPromedio = reader.IsDBNull(7) ? (int?)null : reader.GetInt32(7)
                    });
                }
            }

            return Ok(new { success = true, data = debilidades });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo debilidades");
            return StatusCode(500, new { success = false, message = "Error interno" });
        }
    }

    // ═══════════════════════════════════════════════════════
    // 📈 GET: Progreso de un Tema Específico
    // ═══════════════════════════════════════════════════════
    [HttpGet("tema/{studentId}/{temaId}")]
    public async Task<ActionResult> GetTemaProgress(int studentId, int temaId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    t.nombre as tema,
                    vrt.total_intentos,
                    vrt.total_correctas,
                    vrt.tasa_acierto,
                    vrt.nivel_dominio,
                    vrt.racha_aciertos_consecutivos,
                    vrt.racha_aciertos_maxima,
                    vrt.dias_sin_practicar,
                    vrt.fecha_primer_intento,
                    vrt.ultima_practica
                FROM vista_rendimiento_temas_completo vrt
                INNER JOIN temas t ON vrt.tema_id = t.id
                WHERE vrt.estudiante_id = $1 AND vrt.tema_id = $2";

            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });
            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = temaId });

            using (var reader = await command.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    return Ok(new
                    {
                        success = true,
                        data = new
                        {
                            tema = reader.GetString(0),
                            intentos = reader.GetInt32(1),
                            correctas = reader.GetInt32(2),
                            tasaAcierto = reader.GetDecimal(3),
                            nivelDominio = reader.GetString(4),
                            rachaActual = reader.GetInt32(5),
                            rachaMaxima = reader.GetInt32(6),
                            diasSinPracticar = reader.IsDBNull(7) ? 0 : reader.GetDouble(7),
                            primerIntento = reader.GetDateTime(8),
                            ultimaPractica = reader.IsDBNull(9) ? (DateTime?)null : reader.GetDateTime(9)
                        }
                    });
                }
            }

            return NotFound(new { success = false, message = "No hay datos para este tema" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo progreso del tema");
            return StatusCode(500, new { success = false, message = "Error interno" });
        }
    }

    // ═══════════════════════════════════════════════════════
    // 🎯 GET: Resumen General de Debilidades
    // ═══════════════════════════════════════════════════════
    [HttpGet("resumen/{studentId}")]
    public async Task<ActionResult> GetResumenDebilidades(int studentId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    nivel_dominio,
                    COUNT(*) as cantidad,
                    AVG(tasa_acierto) as promedio_acierto
                FROM vista_rendimiento_temas_completo
                WHERE estudiante_id = $1
                GROUP BY nivel_dominio
                ORDER BY 
                    CASE nivel_dominio
                        WHEN 'critico' THEN 1
                        WHEN 'debil' THEN 2
                        WHEN 'medio' THEN 3
                        WHEN 'bueno' THEN 4
                        WHEN 'excelente' THEN 5
                    END";

            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });

            var resumen = new List<object>();

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    resumen.Add(new
                    {
                        nivel = reader.GetString(0),
                        cantidad = Convert.ToInt32(reader.GetInt64(1)),
                        promedioAcierto = reader.GetDecimal(2)
                    });
                }
            }

            return Ok(new { success = true, data = resumen });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo resumen");
            return StatusCode(500, new { success = false, message = "Error interno" });
        }
    }

    // ═══════════════════════════════════════════════════════
    // 📚 GET: Debilidades por Subtema
    // ═══════════════════════════════════════════════════════
    [HttpGet("subtemas/{studentId}")]
    public async Task<ActionResult> GetDebilidadesSubtemas(int studentId, [FromQuery] int limit = 10)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    area, tema, subtema, total_intentos, tasa_acierto, 
                    nivel_dominio, dias_sin_practicar, racha_aciertos_consecutivos
                FROM vista_debilidades_subtemas
                WHERE estudiante_id = $1
                  AND nivel_dominio IN ('critico', 'debil', 'medio')
                ORDER BY 
                    CASE nivel_dominio
                        WHEN 'critico' THEN 1
                        WHEN 'debil' THEN 2
                        WHEN 'medio' THEN 3
                    END,
                    tasa_acierto ASC
                LIMIT $2";

            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });
            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = limit });

            var debilidades = new List<object>();

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    debilidades.Add(new
                    {
                        area = reader.GetString(0),
                        tema = reader.GetString(1),
                        subtema = reader.GetString(2),
                        intentos = reader.GetInt32(3),
                        tasaAcierto = reader.GetDecimal(4),
                        nivelDominio = reader.GetString(5),
                        diasSinPracticar = reader.IsDBNull(6) ? 0 : reader.GetDouble(6),
                        racha = reader.GetInt32(7)
                    });
                }
            }

            return Ok(new { success = true, data = debilidades });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo debilidades por subtema");
            return StatusCode(500, new { success = false, message = "Error interno" });
        }
    }

    // ═══════════════════════════════════════════════════════
    // 🔥 GET: Top 5 Temas Más Débiles (Dashboard)
    // ═══════════════════════════════════════════════════════
    [HttpGet("top-debiles/{studentId}")]
    public async Task<ActionResult> GetTopTemasDebiles(int studentId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    tema, tasa_acierto, nivel_dominio, total_intentos
                FROM vista_debilidades_temas
                WHERE estudiante_id = $1
                ORDER BY tasa_acierto ASC
                LIMIT 5";

            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });

            var topDebiles = new List<object>();

            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    topDebiles.Add(new
                    {
                        tema = reader.GetString(0),
                        tasaAcierto = reader.GetDecimal(1),
                        nivelDominio = reader.GetString(2),
                        intentos = reader.GetInt32(3)
                    });
                }
            }

            return Ok(new { success = true, data = topDebiles });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo top temas débiles");
            return StatusCode(500, new { success = false, message = "Error interno" });
        }
    }
}