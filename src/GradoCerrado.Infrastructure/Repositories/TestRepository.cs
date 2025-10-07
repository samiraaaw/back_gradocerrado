// src/GradoCerrado.Infrastructure/Repositories/TestRepository.cs
using GradoCerrado.Application.Interfaces;
using GradoCerrado.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace GradoCerrado.Infrastructure.Repositories;

public class TestRepository : ITestRepository
{
    private readonly GradocerradoContext _context;

    public TestRepository(GradocerradoContext context)
    {
        _context = context;
    }

    public async Task<Test?> GetByIdAsync(int id)
    {
        // ✅ Este método puede seguir usando EF porque la tabla 'tests' está bien configurada
        return await _context.Tests
            .Include(t => t.Modalidad)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    // ✅ MÉTODO CORREGIDO: Usar SQL directo en lugar de EF
    public async Task<TestPregunta?> GetTestPreguntaAsync(int testId, short numeroOrden)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                id, 
                test_id, 
                pregunta_generada_id, 
                respuesta_texto, 
                respuesta_boolean, 
                respuesta_opcion, 
                es_correcta, 
                tiempo_respuesta_segundos, 
                fecha_respuesta, 
                numero_orden
            FROM test_preguntas 
            WHERE test_id = $1 AND numero_orden = $2";

        command.Parameters.Add(new NpgsqlParameter { Value = testId });
        command.Parameters.Add(new NpgsqlParameter { Value = numeroOrden });

        using var reader = await command.ExecuteReaderAsync();

        if (await reader.ReadAsync())
        {
            return new TestPregunta
            {
                Id = reader.GetInt32(0),
                TestId = reader.GetInt32(1),
                PreguntaGeneradaId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                RespuestaTexto = reader.IsDBNull(3) ? null : reader.GetString(3),
                RespuestaBoolean = reader.IsDBNull(4) ? null : reader.GetBoolean(4),
                RespuestaOpcion = reader.IsDBNull(5) ? null : reader.GetChar(5),
                EsCorrecta = reader.IsDBNull(6) ? null : reader.GetBoolean(6),
                TiempoRespuestaSegundos = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                FechaRespuesta = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                NumeroOrden = reader.GetInt16(9)
            };
        }

        return null;
    }

    // ✅ MÉTODO CORREGIDO: Usar SQL directo
    public async Task<TestPregunta> CreateTestPreguntaAsync(TestPregunta testPregunta)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO test_preguntas 
            (test_id, pregunta_generada_id, respuesta_texto, respuesta_boolean, respuesta_opcion, 
             es_correcta, tiempo_respuesta_segundos, fecha_respuesta, numero_orden)
            VALUES 
            ($1, $2, $3, $4, $5, $6, $7, $8, $9)
            RETURNING id";

        command.Parameters.Add(new NpgsqlParameter { Value = testPregunta.TestId });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.PreguntaGeneradaId ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.RespuestaTexto ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.RespuestaBoolean ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.RespuestaOpcion ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.EsCorrecta ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.TiempoRespuestaSegundos ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.FechaRespuesta ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = testPregunta.NumeroOrden });

        testPregunta.Id = Convert.ToInt32(await command.ExecuteScalarAsync());

        return testPregunta;
    }

    public System.Data.Common.DbConnection GetDbConnection()
    {
        return _context.Database.GetDbConnection();
    }

    // ✅ MÉTODO CORREGIDO: Usar SQL directo
    public async Task UpdateTestPreguntaAsync(TestPregunta testPregunta)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE test_preguntas 
            SET respuesta_texto = $1,
                respuesta_boolean = $2,
                respuesta_opcion = $3,
                es_correcta = $4,
                tiempo_respuesta_segundos = $5,
                fecha_respuesta = $6
            WHERE id = $7";

        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.RespuestaTexto ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.RespuestaBoolean ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.RespuestaOpcion ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.EsCorrecta ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.TiempoRespuestaSegundos ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = (object?)testPregunta.FechaRespuesta ?? DBNull.Value });
        command.Parameters.Add(new NpgsqlParameter { Value = testPregunta.Id });

        await command.ExecuteNonQueryAsync();
    }

    // ✅ MÉTODO CORREGIDO: Usar SQL directo
    public async Task<List<TestPregunta>> GetTestPreguntasByTestIdAsync(int testId)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync();

        var preguntas = new List<TestPregunta>();

        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT 
                tp.id, 
                tp.test_id, 
                tp.pregunta_generada_id, 
                tp.respuesta_texto, 
                tp.respuesta_boolean, 
                tp.respuesta_opcion, 
                tp.es_correcta, 
                tp.tiempo_respuesta_segundos, 
                tp.fecha_respuesta, 
                tp.numero_orden
            FROM test_preguntas tp
            WHERE tp.test_id = $1
            ORDER BY tp.numero_orden";

        command.Parameters.Add(new NpgsqlParameter { Value = testId });

        using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            preguntas.Add(new TestPregunta
            {
                Id = reader.GetInt32(0),
                TestId = reader.GetInt32(1),
                PreguntaGeneradaId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
                RespuestaTexto = reader.IsDBNull(3) ? null : reader.GetString(3),
                RespuestaBoolean = reader.IsDBNull(4) ? null : reader.GetBoolean(4),
                RespuestaOpcion = reader.IsDBNull(5) ? null : reader.GetChar(5),
                EsCorrecta = reader.IsDBNull(6) ? null : reader.GetBoolean(6),
                TiempoRespuestaSegundos = reader.IsDBNull(7) ? null : reader.GetInt32(7),
                FechaRespuesta = reader.IsDBNull(8) ? null : reader.GetDateTime(8),
                NumeroOrden = reader.GetInt16(9)
            });
        }

        return preguntas;
    }
}