// src/GradoCerrado.Application/Services/MetricasEstudianteService.cs
using GradoCerrado.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GradoCerrado.Application.Services;

public interface IMetricasEstudianteService
{
    Task ActualizarMetricasAsync(int estudianteId);
    Task<MetricasEstudiante> ObtenerMetricasAsync(int estudianteId);
    Task RecalcularMetricasGlobalesAsync();
}

public class MetricasEstudianteService : IMetricasEstudianteService
{
    private readonly GradocerradoContext _context;
    private readonly ILogger<MetricasEstudianteService> _logger;

    public MetricasEstudianteService(
        GradocerradoContext context,
        ILogger<MetricasEstudianteService> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Actualiza las métricas de un estudiante después de completar un test
    /// </summary>
    public async Task ActualizarMetricasAsync(int estudianteId)
    {
        try
        {
            _logger.LogInformation("📊 Actualizando métricas para estudiante {Id}", estudianteId);

            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            // ═══════════════════════════════════════════════════════════
            // 1️⃣ CALCULAR DÍAS CON ACTIVIDAD
            // ═══════════════════════════════════════════════════════════
            using var diasCommand = connection.CreateCommand();
            diasCommand.CommandText = @"
                WITH dias_estudio AS (
                    SELECT DISTINCT 
                        DATE(t.fecha_creacion AT TIME ZONE 'UTC' AT TIME ZONE 'America/Santiago') as dia
                    FROM tests t
                    WHERE t.estudiante_id = $1
                      AND t.completado = true
                )
                SELECT 
                    COUNT(*) as total_dias,
                    MIN(dia) as primer_dia,
                    MAX(dia) as ultimo_dia
                FROM dias_estudio";

            diasCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = estudianteId });

            int totalDias = 0;
            DateOnly? primerDia = null;
            DateOnly? ultimoDia = null;

            using (var reader = await diasCommand.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    totalDias = reader.IsDBNull(0) ? 0 : Convert.ToInt32(reader.GetInt64(0));
                    primerDia = reader.IsDBNull(1) ? null : DateOnly.FromDateTime(reader.GetDateTime(1));
                    ultimoDia = reader.IsDBNull(2) ? null : DateOnly.FromDateTime(reader.GetDateTime(2));
                }
            }

            // ═══════════════════════════════════════════════════════════
            // 2️⃣ CALCULAR RACHA ACTUAL
            // ═══════════════════════════════════════════════════════════
            var racha = await CalcularRachaActualAsync(estudianteId);

            // ═══════════════════════════════════════════════════════════
            // 3️⃣ CALCULAR RACHA MÁXIMA
            // ═══════════════════════════════════════════════════════════
            var rachaMaxima = await CalcularRachaMaximaAsync(estudianteId);

            // ═══════════════════════════════════════════════════════════
            // 4️⃣ CALCULAR PROMEDIO DE PREGUNTAS POR DÍA
            // ═══════════════════════════════════════════════════════════
            decimal promedioPreguntas = 0;
            if (totalDias > 0)
            {
                using var preguntasCommand = connection.CreateCommand();
                preguntasCommand.CommandText = @"
                    SELECT COUNT(*) 
                    FROM test_preguntas tp
                    INNER JOIN tests t ON t.id = tp.test_id
                    WHERE t.estudiante_id = $1";

                preguntasCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = estudianteId });

                var totalPreguntas = Convert.ToInt32(await preguntasCommand.ExecuteScalarAsync() ?? 0);
                promedioPreguntas = Math.Round((decimal)totalPreguntas / totalDias, 2);
            }

            // ═══════════════════════════════════════════════════════════
            // 5️⃣ CALCULAR PROMEDIO DE ACIERTOS
            // ═══════════════════════════════════════════════════════════
            using var aciertosCommand = connection.CreateCommand();
            aciertosCommand.CommandText = @"
                SELECT 
                    ROUND(
                        CASE 
                            WHEN COUNT(tp.id) > 0 
                            THEN (SUM(CASE WHEN tp.es_correcta = true THEN 1 ELSE 0 END)::numeric / COUNT(tp.id)::numeric) * 100
                            ELSE 0 
                        END, 2
                    ) as promedio_aciertos
                FROM test_preguntas tp
                INNER JOIN tests t ON t.id = tp.test_id
                WHERE t.estudiante_id = $1";

            aciertosCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = estudianteId });

            var promedioAciertos = Convert.ToDecimal(await aciertosCommand.ExecuteScalarAsync() ?? 0m);

            // ═══════════════════════════════════════════════════════════
            // 6️⃣ ACTUALIZAR O CREAR REGISTRO DE MÉTRICAS
            // ═══════════════════════════════════════════════════════════
            var metricas = await _context.Set<MetricasEstudiante>()
                .FirstOrDefaultAsync(m => m.EstudianteId == estudianteId);

            if (metricas == null)
            {
                metricas = new MetricasEstudiante
                {
                    EstudianteId = estudianteId,
                    RachaDiasActual = racha,
                    RachaDiasMaxima = rachaMaxima,
                    UltimoDiaEstudio = ultimoDia,
                    PrimeraFechaEstudio = primerDia,
                    TotalDiasEstudiados = totalDias,
                    PromedioPreguntasDia = promedioPreguntas,
                    PromedioAciertos = promedioAciertos,
                    FechaActualizacion = DateTime.UtcNow,
                    VersionCalculo = 1
                };

                _context.Set<MetricasEstudiante>().Add(metricas);
            }
            else
            {
                metricas.RachaDiasActual = racha;
                metricas.RachaDiasMaxima = Math.Max(rachaMaxima, metricas.RachaDiasMaxima ?? 0);
                metricas.UltimoDiaEstudio = ultimoDia;
                metricas.TotalDiasEstudiados = totalDias;
                metricas.PromedioPreguntasDia = promedioPreguntas;
                metricas.PromedioAciertos = promedioAciertos;
                metricas.FechaActualizacion = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "✅ Métricas actualizadas: Racha={Racha}, Total Días={Dias}, Promedio Aciertos={Aciertos}%",
                racha, totalDias, promedioAciertos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error actualizando métricas");
            throw;
        }
    }

    /// <summary>
    /// Obtiene las métricas actuales de un estudiante
    /// </summary>
    public async Task<MetricasEstudiante> ObtenerMetricasAsync(int estudianteId)
    {
        try
        {
            var metricas = await _context.Set<MetricasEstudiante>()
                .FirstOrDefaultAsync(m => m.EstudianteId == estudianteId);

            if (metricas == null)
            {
                // Si no existen métricas, calcularlas por primera vez
                await ActualizarMetricasAsync(estudianteId);
                metricas = await _context.Set<MetricasEstudiante>()
                    .FirstOrDefaultAsync(m => m.EstudianteId == estudianteId);
            }

            return metricas ?? new MetricasEstudiante { EstudianteId = estudianteId };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo métricas");
            throw;
        }
    }

    /// <summary>
    /// Recalcula las métricas de todos los estudiantes (tarea de mantenimiento)
    /// </summary>
    public async Task RecalcularMetricasGlobalesAsync()
    {
        try
        {
            _logger.LogInformation("🔄 Iniciando recálculo global de métricas");

            var estudiantes = await _context.Estudiantes
                .Where(e => e.Activo == true)
                .Select(e => e.Id)
                .ToListAsync();

            var procesados = 0;
            var errores = 0;

            foreach (var estudianteId in estudiantes)
            {
                try
                {
                    await ActualizarMetricasAsync(estudianteId);
                    procesados++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error procesando estudiante {Id}", estudianteId);
                    errores++;
                }
            }

            _logger.LogInformation(
                "✅ Recálculo completado: {Procesados} exitosos, {Errores} errores",
                procesados, errores);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en recálculo global");
            throw;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // MÉTODOS PRIVADOS
    // ═══════════════════════════════════════════════════════════

    private async Task<int> CalcularRachaActualAsync(int estudianteId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                WITH dias_estudio AS (
                    SELECT DISTINCT 
                        DATE(t.fecha_creacion AT TIME ZONE 'UTC' AT TIME ZONE 'America/Santiago') as dia
                    FROM tests t
                    WHERE t.estudiante_id = $1
                      AND t.completado = true
                    ORDER BY dia DESC
                ),
                dias_consecutivos AS (
                    SELECT 
                        dia,
                        dia - ROW_NUMBER() OVER (ORDER BY dia DESC)::int as grupo_racha
                    FROM dias_estudio
                )
                SELECT COUNT(*) as racha
                FROM dias_consecutivos
                WHERE grupo_racha = (
                    SELECT grupo_racha 
                    FROM dias_consecutivos 
                    WHERE dia = (SELECT MAX(dia) FROM dias_estudio)
                )";

            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = estudianteId });

            var racha = Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);

            // Validar que la racha sea actual (último día debe ser hoy o ayer)
            using var validacionCommand = connection.CreateCommand();
            validacionCommand.CommandText = @"
                SELECT MAX(DATE(t.fecha_creacion AT TIME ZONE 'UTC' AT TIME ZONE 'America/Santiago'))
                FROM tests t
                WHERE t.estudiante_id = $1 AND t.completado = true";

            validacionCommand.Parameters.Add(new Npgsql.NpgsqlParameter { Value = estudianteId });

            var ultimoDia = await validacionCommand.ExecuteScalarAsync();

            if (ultimoDia != null && ultimoDia != DBNull.Value)
            {
                var ultimaFecha = (DateTime)ultimoDia;
                var diasDesdeUltimoEstudio = (DateTime.Now.Date - ultimaFecha.Date).Days;

                // Si han pasado más de 1 día, la racha se pierde
                if (diasDesdeUltimoEstudio > 1)
                {
                    racha = 0;
                }
            }

            return racha;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculando racha actual");
            return 0;
        }
    }

    private async Task<int> CalcularRachaMaximaAsync(int estudianteId)
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                WITH dias_estudio AS (
                    SELECT DISTINCT 
                        DATE(t.fecha_creacion AT TIME ZONE 'UTC' AT TIME ZONE 'America/Santiago') as dia
                    FROM tests t
                    WHERE t.estudiante_id = $1
                      AND t.completado = true
                ),
                rachas AS (
                    SELECT 
                        dia,
                        dia - ROW_NUMBER() OVER (ORDER BY dia)::int as grupo_racha
                    FROM dias_estudio
                )
                SELECT MAX(cuenta) as racha_maxima
                FROM (
                    SELECT COUNT(*) as cuenta
                    FROM rachas
                    GROUP BY grupo_racha
                ) subquery";

            command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = estudianteId });

            return Convert.ToInt32(await command.ExecuteScalarAsync() ?? 0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculando racha máxima");
            return 0;
        }
    }
}