using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradoCerrado.Domain.Models;
using GradoCerrado.Application.Interfaces;

namespace GradoCerrado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotificacionesController : ControllerBase
{
    private readonly GradocerradoContext _context;
    private readonly ILogger<NotificacionesController> _logger;
    private readonly IPushNotificationService _pushService;

    public NotificacionesController(
        GradocerradoContext context,
        ILogger<NotificacionesController> logger,
        IPushNotificationService pushService)
    {
        _context = context;
        _logger = logger;
        _pushService = pushService;
    }

    // ═══════════════════════════════════════════════════════════
    // 📱 REGISTRO DE TOKEN DE DISPOSITIVO
    // ═══════════════════════════════════════════════════════════

    [HttpPost("{estudianteId}/register-device")]
    public async Task<ActionResult> RegisterDeviceToken(
        int estudianteId,
        [FromBody] RegisterDeviceRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Token))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Token es requerido"
                });
            }

            var config = await _context.EstudianteNotificacionConfigs
                .FirstOrDefaultAsync(c => c.EstudianteId == estudianteId);

            if (config == null)
            {
                config = new EstudianteNotificacionConfig
                {
                    EstudianteId = estudianteId,
                    TokenDispositivo = request.Token,
                    NotificacionesHabilitadas = true,
                    FechaActualizacion = DateTime.UtcNow
                };

                _context.EstudianteNotificacionConfigs.Add(config);
            }
            else
            {
                config.TokenDispositivo = request.Token;
                config.FechaActualizacion = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "✅ Token registrado para estudiante {Id}",
                estudianteId);

            return Ok(new
            {
                success = true,
                message = "Token registrado exitosamente"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando token");
            return StatusCode(500, new
            {
                success = false,
                message = "Error interno"
            });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 🔔 ENVIAR NOTIFICACIÓN DE PRUEBA
    // ═══════════════════════════════════════════════════════════

    [HttpPost("{estudianteId}/test-push")]
    public async Task<ActionResult> SendTestPush(int estudianteId)
    {
        try
        {
            var config = await _context.EstudianteNotificacionConfigs
                .FirstOrDefaultAsync(c => c.EstudianteId == estudianteId);

            if (config == null || string.IsNullOrWhiteSpace(config.TokenDispositivo))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "No hay token registrado para este estudiante"
                });
            }

            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Id == estudianteId);

            var enviada = await _pushService.SendPushNotificationAsync(
                deviceToken: config.TokenDispositivo,
                title: "🎯 Notificación de prueba",
                body: $"¡Hola {estudiante?.Nombre}! Las notificaciones están funcionando correctamente 🎉",
                data: new Dictionary<string, string>
                {
                    ["tipo"] = "test",
                    ["timestamp"] = DateTime.UtcNow.ToString("O")
                });

            if (enviada)
            {
                return Ok(new
                {
                    success = true,
                    message = "Notificación de prueba enviada"
                });
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Error enviando notificación"
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error enviando notificación de prueba");
            return StatusCode(500, new
            {
                success = false,
                message = "Error interno"
            });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 📬 VER NOTIFICACIONES
    // ═══════════════════════════════════════════════════════════

    [HttpGet("{estudianteId}")]
    public async Task<ActionResult> GetNotificaciones(int estudianteId)
    {
        try
        {
            var notificaciones = await _context.Notificaciones
                .Where(n => n.EstudianteId == estudianteId)
                .OrderByDescending(n => n.FechaProgramada)
                .Take(50)
                .Select(n => new
                {
                    id = n.Id,
                    titulo = n.Titulo,
                    mensaje = n.Mensaje,
                    fecha = n.FechaProgramada,
                    leido = n.Leido ?? false,
                    enviado = n.Enviado ?? false
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                total = notificaciones.Count,
                noLeidas = notificaciones.Count(n => !n.leido),
                data = notificaciones
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo notificaciones");
            return StatusCode(500, new { success = false, message = "Error" });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // ✅ MARCAR COMO LEÍDA
    // ═══════════════════════════════════════════════════════════

    [HttpPut("{notificacionId}/leer")]
    public async Task<ActionResult> MarcarLeida(int notificacionId)
    {
        try
        {
            var notif = await _context.Notificaciones
                .FirstOrDefaultAsync(n => n.Id == notificacionId);

            if (notif == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "No encontrada"
                });
            }

            notif.Leido = true;
            notif.FechaLeido = DateTime.SpecifyKind(
                DateTime.Now,
                DateTimeKind.Unspecified);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Marcada como leída"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marcando notificación");
            return StatusCode(500, new { success = false, message = "Error" });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // 🔔 CONTADOR DE NO LEÍDAS
    // ═══════════════════════════════════════════════════════════

    [HttpGet("{estudianteId}/contador")]
    public async Task<ActionResult> GetContadorNoLeidas(int estudianteId)
    {
        try
        {
            var noLeidas = await _context.Notificaciones
                .CountAsync(n =>
                    n.EstudianteId == estudianteId &&
                    n.Leido == false);

            return Ok(new
            {
                success = true,
                noLeidas = noLeidas
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error contando notificaciones");
            return Ok(new { success = true, noLeidas = 0 });
        }
    }

    // ═══════════════════════════════════════════════════════════
    // ⚙️ CONFIGURACIÓN DE NOTIFICACIONES
    // ═══════════════════════════════════════════════════════════

    [HttpPut("{estudianteId}/config")]
    public async Task<ActionResult> UpdateNotificationConfig(
        int estudianteId,
        [FromBody] UpdateNotificationConfigRequest request)
    {
        try
        {
            var config = await _context.EstudianteNotificacionConfigs
                .FirstOrDefaultAsync(c => c.EstudianteId == estudianteId);

            if (config == null)
            {
                config = new EstudianteNotificacionConfig
                {
                    EstudianteId = estudianteId,
                    NotificacionesHabilitadas = request.Enabled,
                    FechaActualizacion = DateTime.UtcNow
                };

                _context.EstudianteNotificacionConfigs.Add(config);
            }
            else
            {
                config.NotificacionesHabilitadas = request.Enabled;
                config.FechaActualizacion = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = request.Enabled
                    ? "Notificaciones activadas"
                    : "Notificaciones desactivadas"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error actualizando configuración");
            return StatusCode(500, new { success = false, message = "Error" });
        }
    }
}

// ═══════════════════════════════════════════════════════════
// DTOs
// ═══════════════════════════════════════════════════════════

public class RegisterDeviceRequest
{
    public string Token { get; set; } = string.Empty;
    public string? Platform { get; set; } // "ios" o "android"
}

public class UpdateNotificationConfigRequest
{
    public bool Enabled { get; set; }
}