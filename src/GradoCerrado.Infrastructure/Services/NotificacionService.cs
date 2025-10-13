// src/GradoCerrado.Application/Services/NotificacionService.cs
using GradoCerrado.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GradoCerrado.Application.Services;

public interface INotificacionService
{
    Task GenerarNotificacionesDelDiaAsync();
    Task<List<Notificacion>> ObtenerNotificacionesPendientesAsync(int estudianteId);
    Task MarcarComoLeidaAsync(int notificacionId);
    Task MarcarAccionTomadaAsync(int notificacionId);
}

public class NotificacionService : INotificacionService
{
    private readonly GradocerradoContext _context;
    private readonly ILogger<NotificacionService> _logger;

    public NotificacionService(
        GradocerradoContext context,
        ILogger<NotificacionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Genera notificaciones de recordatorio para todos los estudiantes según su configuración
    /// Este método debe ejecutarse diariamente (ej: con un job scheduler)
    /// </summary>
    public async Task GenerarNotificacionesDelDiaAsync()
    {
        try
        {
            var hoy = DateTime.Now;
            var diaSemana = ObtenerDiaSemanaEspanol(hoy.DayOfWeek);

            _logger.LogInformation(
                "🔔 Generando notificaciones para {Fecha} ({Dia})",
                hoy.ToString("yyyy-MM-dd"), diaSemana);

            // Obtener estudiantes con recordatorios activos
            var estudiantes = await _context.Estudiantes
                .Where(e => e.Activo == true && e.RecordatorioEstudioActivo == true)
                .ToListAsync();

            var notificacionesCreadas = 0;

            foreach (var estudiante in estudiantes)
            {
                // Verificar si hoy es un día de estudio para este estudiante
                if (!DebeRecibirNotificacionHoy(estudiante, diaSemana))
                {
                    _logger.LogDebug(
                        "Estudiante {Id} no recibe notificación hoy ({Dia})",
                        estudiante.Id, diaSemana);
                    continue;
                }

                // Verificar si ya existe una notificación para hoy
                var yaExisteNotificacion = await _context.Notificaciones
                    .AnyAsync(n =>
                        n.EstudianteId == estudiante.Id &&
                        n.FechaProgramada.Date == hoy.Date &&
                        n.TiposNotificacionId == 1); // 1 = Recordatorio de estudio

                if (yaExisteNotificacion)
                {
                    _logger.LogDebug(
                        "Estudiante {Id} ya tiene notificación programada para hoy",
                        estudiante.Id);
                    continue;
                }

                // Crear notificación
                var horaProgramada = estudiante.HoraRecordatorio ?? new TimeOnly(19, 0);
                var fechaProgramada = hoy.Date.Add(horaProgramada.ToTimeSpan());

                var notificacion = new Notificacion
                {
                    EstudianteId = estudiante.Id,
                    TiposNotificacionId = 1, // Recordatorio de estudio
                    Titulo = "⏰ Recordatorio de estudio",
                    Mensaje = GenerarMensajeRecordatorio(estudiante),
                    DatosAdicionales = JsonSerializer.Serialize(new
                    {
                        tipo = "recordatorio_estudio",
                        diaSemana = diaSemana,
                        frecuencia = estudiante.FrecuenciaEstudioSemanal
                    }),
                    FechaProgramada = DateTime.SpecifyKind(fechaProgramada, DateTimeKind.Unspecified),
                    Enviado = false,
                    Leido = false,
                    AccionTomada = false,
                    FechaCreacion = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)
                };

                _context.Notificaciones.Add(notificacion);
                notificacionesCreadas++;

                _logger.LogInformation(
                    "✅ Notificación creada para estudiante {Id} a las {Hora}",
                    estudiante.Id, horaProgramada.ToString(@"hh\:mm"));
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "✅ Proceso completado: {Total} notificaciones creadas",
                notificacionesCreadas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error generando notificaciones del día");
            throw;
        }
    }

    /// <summary>
    /// Obtiene notificaciones pendientes de un estudiante
    /// </summary>
    public async Task<List<Notificacion>> ObtenerNotificacionesPendientesAsync(int estudianteId)
    {
        try
        {
            var ahora = DateTime.Now;

            var notificaciones = await _context.Notificaciones
                .Where(n =>
                    n.EstudianteId == estudianteId &&
                    n.Leido == false &&
                    n.FechaProgramada <= ahora)
                .OrderByDescending(n => n.FechaProgramada)
                .Take(10)
                .ToListAsync();

            return notificaciones;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo notificaciones pendientes");
            return new List<Notificacion>();
        }
    }

    /// <summary>
    /// Marca una notificación como leída
    /// </summary>
    public async Task MarcarComoLeidaAsync(int notificacionId)
    {
        try
        {
            var notificacion = await _context.Notificaciones
                .FirstOrDefaultAsync(n => n.Id == notificacionId);

            if (notificacion != null)
            {
                notificacion.Leido = true;
                notificacion.FechaLeido = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Notificación {Id} marcada como leída", notificacionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marcando notificación como leída");
            throw;
        }
    }

    /// <summary>
    /// Marca que el usuario tomó acción sobre la notificación (ej: inició sesión de estudio)
    /// </summary>
    public async Task MarcarAccionTomadaAsync(int notificacionId)
    {
        try
        {
            var notificacion = await _context.Notificaciones
                .FirstOrDefaultAsync(n => n.Id == notificacionId);

            if (notificacion != null)
            {
                notificacion.AccionTomada = true;
                notificacion.FechaAccion = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Acción tomada en notificación {Id}", notificacionId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marcando acción tomada");
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // MÉTODOS PRIVADOS
    // ═══════════════════════════════════════════════════════════

    private bool DebeRecibirNotificacionHoy(Estudiante estudiante, string diaSemana)
    {
        // Si no tiene días preferidos configurados, usar frecuencia semanal
        if (string.IsNullOrEmpty(estudiante.DiasPreferidosEstudio))
        {
            // Estrategia simple: distribuir días según frecuencia
            var frecuencia = estudiante.FrecuenciaEstudioSemanal ?? 3;

            // Ejemplo: Si frecuencia = 3, enviar Lun/Mié/Vie
            var diasDistribuidos = DistribuirDiasPorFrecuencia(frecuencia);
            return diasDistribuidos.Contains(diaSemana);
        }

        // Si tiene días preferidos configurados, verificar si hoy es uno de ellos
        try
        {
            var diasPreferidos = JsonSerializer.Deserialize<List<string>>(
                estudiante.DiasPreferidosEstudio);

            return diasPreferidos?.Contains(diaSemana.ToLower()) ?? false;
        }
        catch
        {
            _logger.LogWarning(
                "Error parseando días preferidos para estudiante {Id}",
                estudiante.Id);
            return false;
        }
    }

    private List<string> DistribuirDiasPorFrecuencia(int frecuencia)
    {
        // Distribución estándar de días según frecuencia
        return frecuencia switch
        {
            1 => new List<string> { "miercoles" },
            2 => new List<string> { "martes", "jueves" },
            3 => new List<string> { "lunes", "miercoles", "viernes" },
            4 => new List<string> { "lunes", "martes", "jueves", "viernes" },
            5 => new List<string> { "lunes", "martes", "miercoles", "jueves", "viernes" },
            6 => new List<string> { "lunes", "martes", "miercoles", "jueves", "viernes", "sabado" },
            7 => new List<string> { "lunes", "martes", "miercoles", "jueves", "viernes", "sabado", "domingo" },
            _ => new List<string> { "lunes", "miercoles", "viernes" }
        };
    }

    private string GenerarMensajeRecordatorio(Estudiante estudiante)
    {
        var mensajes = new[]
        {
            $"¡Hola {estudiante.Nombre}! Es hora de tu sesión de estudio de hoy 📚",
            $"{estudiante.Nombre}, ¡no olvides tu práctica de hoy! 💪",
            $"Recordatorio: Tu sesión de estudio te espera, {estudiante.Nombre} 🎯",
            $"¡Es momento de practicar, {estudiante.Nombre}! Mantén tu racha activa 🔥",
            $"Tu aprendizaje diario está listo, {estudiante.Nombre} ✨"
        };

        var random = new Random();
        return mensajes[random.Next(mensajes.Length)];
    }

    private string ObtenerDiaSemanaEspanol(DayOfWeek dayOfWeek)
    {
        return dayOfWeek switch
        {
            DayOfWeek.Monday => "lunes",
            DayOfWeek.Tuesday => "martes",
            DayOfWeek.Wednesday => "miercoles",
            DayOfWeek.Thursday => "jueves",
            DayOfWeek.Friday => "viernes",
            DayOfWeek.Saturday => "sabado",
            DayOfWeek.Sunday => "domingo",
            _ => "lunes"
        };
    }
}