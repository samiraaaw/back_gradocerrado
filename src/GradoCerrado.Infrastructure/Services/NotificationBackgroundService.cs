using GradoCerrado.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore; // ✅ AGREGAR ESTA LÍNEA
using GradoCerrado.Application.Interfaces; // ✅ AGREGAR ESTA LÍNEA
using GradoCerrado.Domain.Models; // ✅ AGREGAR ESTA LÍNEA

namespace GradoCerrado.Infrastructure.Services;

/// <summary>
/// Servicio en background que ejecuta tareas programadas de notificaciones
/// </summary>
public class NotificationBackgroundService : BackgroundService
{
    private readonly ILogger<NotificationBackgroundService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private Timer? _timer;

    public NotificationBackgroundService(
        ILogger<NotificationBackgroundService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🔔 Servicio de Notificaciones iniciado");

        // Ejecutar cada hora
        _timer = new Timer(
            callback: async _ => await DoWorkAsync(),
            state: null,
            dueTime: TimeSpan.Zero, // Ejecutar inmediatamente al iniciar
            period: TimeSpan.FromHours(1)); // Luego cada hora

        return Task.CompletedTask;
    }

    private async Task DoWorkAsync()
    {
        try
        {
            var horaActual = DateTime.Now.Hour;

            _logger.LogDebug("⏰ Verificando si es hora de generar notificaciones (hora actual: {Hora})", horaActual);

            // Solo generar notificaciones a las 6 AM
            if (horaActual == 6)
            {
                _logger.LogInformation("🔔 Iniciando generación de notificaciones del día");

                using var scope = _serviceProvider.CreateScope();
                var notificationService = scope.ServiceProvider
                    .GetRequiredService<INotificacionService>();

                await notificationService.GenerarNotificacionesDelDiaAsync();

                _logger.LogInformation("✅ Notificaciones generadas exitosamente");
            }

            // Enviar notificaciones pendientes cada hora
            await EnviarNotificacionesPendientesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error en tarea programada de notificaciones");
        }
    }

    private async Task EnviarNotificacionesPendientesAsync()
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();

            var pushService = scope.ServiceProvider
                .GetRequiredService<IPushNotificationService>();

            var context = scope.ServiceProvider
                .GetRequiredService<GradocerradoContext>();

            // ✅ CORRECCIÓN: Usar DateTime sin zona horaria
            var ahora = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

            // ✅ CORRECCIÓN: Agregar OrderBy antes de Take
            var notificacionesPendientes = await context.Notificaciones
                .Where(n =>
                    n.Enviado == false &&
                    n.FechaProgramada <= ahora)
                .OrderBy(n => n.FechaProgramada) // ✅ AGREGAR ESTA LÍNEA
                .Take(50)
                .ToListAsync();

            if (!notificacionesPendientes.Any())
            {
                _logger.LogDebug("No hay notificaciones pendientes por enviar");
                return;
            }

            _logger.LogInformation(
                "📤 Enviando {Count} notificaciones pendientes",
                notificacionesPendientes.Count);

            foreach (var notificacion in notificacionesPendientes)
            {
                try
                {
                    // Obtener token del estudiante
                    var config = await context.EstudianteNotificacionConfigs
                        .FirstOrDefaultAsync(c =>
                            c.EstudianteId == notificacion.EstudianteId &&
                            c.NotificacionesHabilitadas == true);

                    if (config == null || string.IsNullOrWhiteSpace(config.TokenDispositivo))
                    {
                        _logger.LogDebug(
                            "Estudiante {Id} no tiene token registrado",
                            notificacion.EstudianteId);
                        continue;
                    }

                    // Enviar notificación push
                    var enviada = await pushService.SendPushNotificationAsync(
                        deviceToken: config.TokenDispositivo,
                        title: notificacion.Titulo,
                        body: notificacion.Mensaje,
                        data: new Dictionary<string, string>
                        {
                            ["notificacion_id"] = notificacion.Id.ToString(),
                            ["tipo"] = notificacion.TiposNotificacionId.ToString()
                        });

                    if (enviada)
                    {
                        notificacion.Enviado = true;
                        notificacion.FechaEnviado = DateTime.SpecifyKind(
                            DateTime.Now,
                            DateTimeKind.Unspecified);

                        _logger.LogInformation(
                            "✅ Notificación {Id} enviada a estudiante {EstudianteId}",
                            notificacion.Id, notificacion.EstudianteId);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Error enviando notificación {Id}",
                        notificacion.Id);
                }
            }

            await context.SaveChangesAsync();

            _logger.LogInformation("✅ Proceso de envío completado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en envío de notificaciones pendientes");
        }
    }



    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }
}
