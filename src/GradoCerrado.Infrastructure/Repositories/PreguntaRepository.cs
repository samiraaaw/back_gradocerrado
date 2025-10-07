// src/GradoCerrado.Infrastructure/Repositories/PreguntaRepository.cs
using GradoCerrado.Application.Interfaces;
using GradoCerrado.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GradoCerrado.Infrastructure.Repositories;

public class PreguntaRepository : IPreguntaRepository
{
    private readonly GradocerradoContext _context;

    public PreguntaRepository(GradocerradoContext context)
    {
        _context = context;
    }

    // ✅ MÉTODO CORREGIDO: Usar SQL directo
    public async Task<PreguntasGenerada?> GetByIdAsync(int id)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                id, tipo, modalidad_id, nivel, tema_id, texto_pregunta,
                respuesta_correcta_boolean, respuesta_correcta_opcion, respuesta_modelo,
                explicacion, activa, creada_por, fecha_creacion, fecha_actualizacion,
                veces_utilizada, veces_correcta, ultimo_uso, modelo_ia, calidad_estimada
            FROM preguntas_generadas
            WHERE id = $1";

        command.Parameters.Add(new NpgsqlParameter { Value = id });

        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new PreguntasGenerada
            {
                Id = reader.GetInt32(0),
                Tipo = reader.GetString(1),
                ModalidadId = reader.GetInt32(2),
                Nivel = reader.GetString(3),
                TemaId = reader.GetInt32(4),
                TextoPregunta = reader.GetString(5),
                RespuestaCorrectaBoolean = reader.IsDBNull(6) ? null : reader.GetBoolean(6),
                RespuestaCorrectaOpcion = reader.IsDBNull(7) ? null : reader.GetChar(7),
                RespuestaModelo = reader.IsDBNull(8) ? null : reader.GetString(8),
                Explicacion = reader.IsDBNull(9) ? null : reader.GetString(9),
                Activa = reader.IsDBNull(10) ? null : reader.GetBoolean(10),
                CreadaPor = reader.IsDBNull(11) ? null : reader.GetString(11),
                FechaCreacion = reader.IsDBNull(12) ? null : reader.GetDateTime(12),
                FechaActualizacion = reader.IsDBNull(13) ? null : reader.GetDateTime(13),
                VecesUtilizada = reader.IsDBNull(14) ? null : reader.GetInt32(14),
                VecesCorrecta = reader.IsDBNull(15) ? null : reader.GetInt32(15),
                UltimoUso = reader.IsDBNull(16) ? null : reader.GetDateTime(16),
                ModeloIa = reader.IsDBNull(17) ? null : reader.GetString(17),
                CalidadEstimada = reader.IsDBNull(18) ? null : reader.GetDecimal(18)
            };
        }

        return null;
    }

    // ✅ MÉTODO CORREGIDO: Usar SQL directo
    public async Task UpdateAsync(PreguntasGenerada pregunta)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE preguntas_generadas 
            SET veces_utilizada = $1,
                veces_correcta = $2,
                ultimo_uso = $3,
                fecha_actualizacion = $4
            WHERE id = $5";

        command.Parameters.Add(new NpgsqlParameter { Value = (object?)pregunta.VecesUtilizada ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)pregunta.VecesCorrecta ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)pregunta.UltimoUso ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)pregunta.FechaActualizacion ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = pregunta.Id });

        await command.ExecuteNonQueryAsync();
    }
}