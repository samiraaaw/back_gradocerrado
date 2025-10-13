// src/GradoCerrado.Api/Controllers/StudyFrequencyController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradoCerrado.Domain.Models;
using System.Text.Json;

namespace GradoCerrado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StudyFrequencyController : ControllerBase
{
    private readonly GradocerradoContext _context;
    private readonly ILogger<StudyFrequencyController> _logger;

    public StudyFrequencyController(GradocerradoContext context, ILogger<StudyFrequencyController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ============================================
    // GET: Obtener configuración de frecuencia
    // ============================================
    [HttpGet("{studentId}")]
    public async Task<ActionResult> GetStudyFrequency(int studentId)
    {
        try
        {
            var estudiante = await _context.Estudiantes
                .Where(e => e.Id == studentId && e.Activo == true)
                .Select(e => new
                {
                    estudianteId = e.Id,
                    frecuenciaSemanal = e.FrecuenciaEstudioSemanal ?? 3,
                    objetivoDias = e.ObjetivoDiasEstudio ?? "flexible",
                    diasPreferidos = e.DiasPreferidosEstudio ?? "[]",
                    recordatorioActivo = e.RecordatorioEstudioActivo ?? true,
                    horaRecordatorio = e.HoraRecordatorio != null
                        ? e.HoraRecordatorio.Value.ToString(@"hh\:mm")
                        : "19:00"
                })
                .FirstOrDefaultAsync();

            if (estudiante == null)
            {
                return NotFound(new { success = false, message = "Estudiante no encontrado" });
            }

            // Parsear días preferidos desde JSON
            List<int> diasList = new List<int>();
            try
            {
                if (!string.IsNullOrEmpty(estudiante.diasPreferidos))
                {
                    diasList = JsonSerializer.Deserialize<List<int>>(estudiante.diasPreferidos) ?? new List<int>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parseando días preferidos para estudiante {StudentId}", studentId);
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    estudiante.estudianteId,
                    estudiante.frecuenciaSemanal,
                    estudiante.objetivoDias,
                    diasPreferidos = diasList,
                    estudiante.recordatorioActivo,
                    estudiante.horaRecordatorio
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo frecuencia de estudio del estudiante {StudentId}", studentId);
            return StatusCode(500, new { success = false, message = "Error interno del servidor" });
        }
    }

    // ============================================
    // PUT: Actualizar configuración de frecuencia
    // ============================================
    [HttpPut("{studentId}")]
    public async Task<ActionResult> UpdateStudyFrequency(int studentId, [FromBody] UpdateFrequencyRequest request)
    {
        try
        {
            // Validaciones
            if (request.FrecuenciaSemanal < 1 || request.FrecuenciaSemanal > 7)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "La frecuencia semanal debe estar entre 1 y 7 días"
                });
            }

            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Id == studentId && e.Activo == true);

            if (estudiante == null)
            {
                return NotFound(new { success = false, message = "Estudiante no encontrado" });
            }

            // Actualizar campos
            estudiante.FrecuenciaEstudioSemanal = request.FrecuenciaSemanal;
            estudiante.ObjetivoDiasEstudio = request.ObjetivoDias ?? "flexible";
            estudiante.RecordatorioEstudioActivo = request.RecordatorioActivo;

            // Actualizar hora de recordatorio
            if (!string.IsNullOrEmpty(request.HoraRecordatorio))
            {
                if (TimeOnly.TryParse(request.HoraRecordatorio, out var horaParseada))
                {
                    estudiante.HoraRecordatorio = horaParseada;
                }
            }

            // Actualizar días preferidos (guardar como JSON)
            if (request.DiasPreferidos != null)
            {
                var diasJson = JsonSerializer.Serialize(request.DiasPreferidos);
                estudiante.DiasPreferidosEstudio = diasJson;
            }

            estudiante.UltimoAcceso = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Frecuencia actualizada para estudiante {StudentId}: {Frecuencia} días/semana",
                studentId,
                request.FrecuenciaSemanal
            );

            return Ok(new
            {
                success = true,
                message = "Frecuencia de estudio actualizada correctamente",
                data = new
                {
                    estudianteId = estudiante.Id,
                    frecuenciaSemanal = estudiante.FrecuenciaEstudioSemanal,
                    objetivoDias = estudiante.ObjetivoDiasEstudio,
                    diasPreferidos = request.DiasPreferidos,
                    recordatorioActivo = estudiante.RecordatorioEstudioActivo,
                    horaRecordatorio = estudiante.HoraRecordatorio?.ToString(@"hh\:mm") ?? "19:00"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando frecuencia del estudiante {StudentId}", studentId);
            return StatusCode(500, new { success = false, message = "Error interno del servidor" });
        }
    }

    // ============================================
    // GET: Obtener cumplimiento de frecuencia
    // ============================================
    [HttpGet("{studentId}/cumplimiento")]
    public async Task<ActionResult> GetCumplimientoFrecuencia(int studentId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT 
                    objetivo_semanal,
                    dias_estudiados_semana_actual,
                    porcentaje_cumplimiento_semanal,
                    racha_actual
                FROM vista_cumplimiento_frecuencia
                WHERE estudiante_id = $1";

            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = studentId });

            using var reader = await command.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                return NotFound(new { success = false, message = "Datos no encontrados" });
            }

            var cumplimiento = new
            {
                objetivoSemanal = reader.GetInt32(0),
                diasEstudiadosSemana = reader.GetInt64(1),
                porcentajeCumplimiento = reader.IsDBNull(2) ? 0m : reader.GetDecimal(2),
                rachaActual = reader.GetInt32(3)
            };

            return Ok(new
            {
                success = true,
                data = cumplimiento
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo cumplimiento del estudiante {StudentId}", studentId);
            return StatusCode(500, new { success = false, message = "Error interno del servidor" });
        }
    }
}

// ============================================
// DTOs
// ============================================
public class UpdateFrequencyRequest
{
    public int FrecuenciaSemanal { get; set; } = 3;
    public string? ObjetivoDias { get; set; } = "flexible";
    public List<int>? DiasPreferidos { get; set; } = new();
    public bool RecordatorioActivo { get; set; } = true;
    public string? HoraRecordatorio { get; set; } = "19:00";
}