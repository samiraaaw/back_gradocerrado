// src/GradoCerrado.Api/Controllers/EstudianteConfigController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradoCerrado.Domain.Models;
using System.Text.Json;

namespace GradoCerrado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EstudianteConfigController : ControllerBase
{
    private readonly GradocerradoContext _context;
    private readonly ILogger<EstudianteConfigController> _logger;

    public EstudianteConfigController(
        GradocerradoContext context,
        ILogger<EstudianteConfigController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    // 📊 GET: Obtener configuración actual del estudiante
    // ═══════════════════════════════════════════════════════════
    [HttpGet("{estudianteId}")]
    public async Task<ActionResult> GetConfiguracion(int estudianteId)
    {
        try
        {
            var estudiante = await _context.Estudiantes
                .Where(e => e.Id == estudianteId && e.Activo == true)
                .Select(e => new
                {
                    e.Id,
                    e.Nombre,
                    e.Email,
                    e.FrecuenciaEstudioSemanal,
                    e.ObjetivoDiasEstudio,
                    e.DiasPreferidosEstudio,
                    e.RecordatorioEstudioActivo,
                    e.HoraRecordatorio
                })
                .FirstOrDefaultAsync();

            if (estudiante == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Estudiante no encontrado"
                });
            }

            // Parsear días preferidos si existen
            List<string>? diasPreferidos = null;
            if (!string.IsNullOrEmpty(estudiante.DiasPreferidosEstudio))
            {
                try
                {
                    diasPreferidos = JsonSerializer.Deserialize<List<string>>(
                        estudiante.DiasPreferidosEstudio);
                }
                catch
                {
                    diasPreferidos = new List<string>();
                }
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    estudianteId = estudiante.Id,
                    nombre = estudiante.Nombre,
                    email = estudiante.Email,
                    frecuenciaEstudioSemanal = estudiante.FrecuenciaEstudioSemanal ?? 3,
                    objetivoDiasEstudio = estudiante.ObjetivoDiasEstudio ?? "flexible",
                    diasPreferidos = diasPreferidos ?? new List<string>(),
                    recordatoriosActivos = estudiante.RecordatorioEstudioActivo ?? true,
                    horaRecordatorio = estudiante.HoraRecordatorio?.ToString(@"hh\:mm") ?? "19:00"
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo configuración");
            return StatusCode(500, new
            {
                success = false,
                message = "Error interno del servidor"
            });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 📝 PUT: Actualizar frecuencia de estudio
    // ═══════════════════════════════════════════════════════════
    [HttpPut("{estudianteId}/frecuencia")]
    public async Task<ActionResult> ActualizarFrecuencia(
        int estudianteId,
        [FromBody] ActualizarFrecuenciaRequest request)
    {
        try
        {
            // ✅ VALIDACIÓN DE FRECUENCIA
            if (request.FrecuenciaSemanal < 1 || request.FrecuenciaSemanal > 7)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "La frecuencia debe estar entre 1 y 7 días por semana"
                });
            }

            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Id == estudianteId && e.Activo == true);

            if (estudiante == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Estudiante no encontrado"
                });
            }

            // Actualizar frecuencia
            estudiante.FrecuenciaEstudioSemanal = request.FrecuenciaSemanal;

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Frecuencia actualizada para estudiante {Id}: {Frecuencia} días/semana",
                estudianteId, request.FrecuenciaSemanal);

            return Ok(new
            {
                success = true,
                message = "Frecuencia de estudio actualizada correctamente",
                data = new
                {
                    estudianteId = estudiante.Id,
                    frecuenciaSemanal = estudiante.FrecuenciaEstudioSemanal
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando frecuencia");
            return StatusCode(500, new
            {
                success = false,
                message = "Error interno del servidor"
            });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 📅 PUT: Actualizar días preferidos de estudio
    // ═══════════════════════════════════════════════════════════
    [HttpPut("{estudianteId}/dias-preferidos")]
    public async Task<ActionResult> ActualizarDiasPreferidos(
        int estudianteId,
        [FromBody] ActualizarDiasPreferidosRequest request)
    {
        try
        {
            // ✅ VALIDACIÓN DE DÍAS
            var diasValidos = new[] { "lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo" };

            if (request.DiasPreferidos == null || !request.DiasPreferidos.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Debe seleccionar al menos un día"
                });
            }

            if (request.DiasPreferidos.Count > 7)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No puede seleccionar más de 7 días"
                });
            }

            // Validar que todos los días sean válidos
            var diasInvalidos = request.DiasPreferidos
                .Where(d => !diasValidos.Contains(d.ToLower()))
                .ToList();

            if (diasInvalidos.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Días inválidos: {string.Join(", ", diasInvalidos)}",
                    diasValidos = diasValidos
                });
            }

            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Id == estudianteId && e.Activo == true);

            if (estudiante == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Estudiante no encontrado"
                });
            }

            // Normalizar días (lowercase) y guardar como JSON
            var diasNormalizados = request.DiasPreferidos
                .Select(d => d.ToLower())
                .Distinct()
                .ToList();

            estudiante.DiasPreferidosEstudio = JsonSerializer.Serialize(diasNormalizados);

            // Actualizar también objetivo según cantidad de días
            estudiante.ObjetivoDiasEstudio = diasNormalizados.Count == 7
                ? "diario"
                : "especifico";

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Días preferidos actualizados para estudiante {Id}: {Dias}",
                estudianteId, string.Join(", ", diasNormalizados));

            return Ok(new
            {
                success = true,
                message = "Días preferidos actualizados correctamente",
                data = new
                {
                    estudianteId = estudiante.Id,
                    diasPreferidos = diasNormalizados,
                    objetivo = estudiante.ObjetivoDiasEstudio
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando días preferidos");
            return StatusCode(500, new
            {
                success = false,
                message = "Error interno del servidor"
            });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 🔔 PUT: Activar/Desactivar recordatorios
    // ═══════════════════════════════════════════════════════════
    [HttpPut("{estudianteId}/recordatorios")]
    public async Task<ActionResult> ActualizarRecordatorios(
        int estudianteId,
        [FromBody] ActualizarRecordatoriosRequest request)
    {
        try
        {
            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Id == estudianteId && e.Activo == true);

            if (estudiante == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Estudiante no encontrado"
                });
            }

            estudiante.RecordatorioEstudioActivo = request.Activo;

            // Si se proporcionó hora, actualizarla
            if (!string.IsNullOrEmpty(request.HoraRecordatorio))
            {
                // ✅ VALIDACIÓN DE FORMATO DE HORA
                if (!TimeOnly.TryParse(request.HoraRecordatorio, out TimeOnly hora))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Formato de hora inválido. Use HH:mm (ej: 19:00)"
                    });
                }

                estudiante.HoraRecordatorio = hora;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Recordatorios {Estado} para estudiante {Id} a las {Hora}",
                request.Activo ? "activados" : "desactivados",
                estudianteId,
                estudiante.HoraRecordatorio?.ToString(@"hh\:mm") ?? "N/A");

            return Ok(new
            {
                success = true,
                message = $"Recordatorios {(request.Activo ? "activados" : "desactivados")} correctamente",
                data = new
                {
                    estudianteId = estudiante.Id,
                    recordatoriosActivos = estudiante.RecordatorioEstudioActivo,
                    horaRecordatorio = estudiante.HoraRecordatorio?.ToString(@"hh\:mm")
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando recordatorios");
            return StatusCode(500, new
            {
                success = false,
                message = "Error interno del servidor"
            });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // ⚙️ PUT: Actualizar configuración completa
    // ═══════════════════════════════════════════════════════════
    [HttpPut("{estudianteId}/completa")]
    public async Task<ActionResult> ActualizarConfiguracionCompleta(
        int estudianteId,
        [FromBody] ActualizarConfiguracionCompletaRequest request)
    {
        try
        {
            // ✅ VALIDACIONES COMPLETAS
            var errores = new List<string>();

            if (request.FrecuenciaSemanal.HasValue)
            {
                if (request.FrecuenciaSemanal < 1 || request.FrecuenciaSemanal > 7)
                {
                    errores.Add("La frecuencia debe estar entre 1 y 7 días por semana");
                }
            }

            if (request.DiasPreferidos != null && request.DiasPreferidos.Any())
            {
                var diasValidos = new[] { "lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo" };
                var diasInvalidos = request.DiasPreferidos
                    .Where(d => !diasValidos.Contains(d.ToLower()))
                    .ToList();

                if (diasInvalidos.Any())
                {
                    errores.Add($"Días inválidos: {string.Join(", ", diasInvalidos)}");
                }
            }

            if (!string.IsNullOrEmpty(request.HoraRecordatorio))
            {
                if (!TimeOnly.TryParse(request.HoraRecordatorio, out _))
                {
                    errores.Add("Formato de hora inválido. Use HH:mm");
                }
            }

            if (errores.Any())
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Errores de validación",
                    errores
                });
            }

            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Id == estudianteId && e.Activo == true);

            if (estudiante == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Estudiante no encontrado"
                });
            }

            // Actualizar campos proporcionados
            if (request.FrecuenciaSemanal.HasValue)
            {
                estudiante.FrecuenciaEstudioSemanal = request.FrecuenciaSemanal.Value;
            }

            if (request.DiasPreferidos != null && request.DiasPreferidos.Any())
            {
                var diasNormalizados = request.DiasPreferidos
                    .Select(d => d.ToLower())
                    .Distinct()
                    .ToList();

                estudiante.DiasPreferidosEstudio = JsonSerializer.Serialize(diasNormalizados);
                estudiante.ObjetivoDiasEstudio = diasNormalizados.Count == 7 ? "diario" : "especifico";
            }

            if (request.RecordatoriosActivos.HasValue)
            {
                estudiante.RecordatorioEstudioActivo = request.RecordatoriosActivos.Value;
            }

            if (!string.IsNullOrEmpty(request.HoraRecordatorio))
            {
                estudiante.HoraRecordatorio = TimeOnly.Parse(request.HoraRecordatorio);
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Configuración completa actualizada para estudiante {Id}",
                estudianteId);

            return Ok(new
            {
                success = true,
                message = "Configuración actualizada correctamente",
                data = new
                {
                    estudianteId = estudiante.Id,
                    frecuenciaSemanal = estudiante.FrecuenciaEstudioSemanal,
                    diasPreferidos = string.IsNullOrEmpty(estudiante.DiasPreferidosEstudio)
                        ? new List<string>()
                        : JsonSerializer.Deserialize<List<string>>(estudiante.DiasPreferidosEstudio),
                    recordatoriosActivos = estudiante.RecordatorioEstudioActivo,
                    horaRecordatorio = estudiante.HoraRecordatorio?.ToString(@"hh\:mm")
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando configuración completa");
            return StatusCode(500, new
            {
                success = false,
                message = "Error interno del servidor"
            });
        }
    }
}

// ═══════════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════════

public class ActualizarFrecuenciaRequest
{
    public int FrecuenciaSemanal { get; set; }
}

public class ActualizarDiasPreferidosRequest
{
    public List<string> DiasPreferidos { get; set; } = new();
}

public class ActualizarRecordatoriosRequest
{
    public bool Activo { get; set; }
    public string? HoraRecordatorio { get; set; }
}

public class ActualizarConfiguracionCompletaRequest
{
    public int? FrecuenciaSemanal { get; set; }
    public List<string>? DiasPreferidos { get; set; }
    public bool? RecordatoriosActivos { get; set; }
    public string? HoraRecordatorio { get; set; }
}