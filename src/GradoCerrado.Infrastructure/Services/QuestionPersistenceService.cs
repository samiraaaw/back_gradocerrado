using GradoCerrado.Application.Interfaces;
using GradoCerrado.Domain.Entities;
using GradoCerrado.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace GradoCerrado.Infrastructure.Services;

public class QuestionPersistenceService : IQuestionPersistenceService
{
    private readonly GradocerradoContext _context;
    private readonly ILogger<QuestionPersistenceService> _logger;

    public QuestionPersistenceService(
        GradocerradoContext context,
        ILogger<QuestionPersistenceService> logger)
    {
        _context = context;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════
    // MÉTODOS PÚBLICOS (implementan la interfaz)
    // ═══════════════════════════════════════════════════════════════

    public async Task<List<int>> SaveQuestionsToDatabase(
       List<StudyQuestion> studyQuestions,
       int temaId,
       int? subtemaId = null,
       int modalidadId = 1,
       string creadaPor = "AI")
    {
        var savedIds = new List<int>();

        try
        {
            const int MODALIDAD_ESCRITO = 1;

            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
                await connection.OpenAsync();

            foreach (var question in studyQuestions)
            {
                string tipoPregunta = question.Type == QuestionType.MultipleChoice
                    ? "seleccion_multiple"
                    : "verdadero_falso";

                string nivelDificultad = question.Difficulty switch
                {
                    DifficultyLevel.Basic => "basico",
                    DifficultyLevel.Intermediate => "intermedio",
                    DifficultyLevel.Advanced => "avanzado",
                    _ => "intermedio"
                };

                bool? respuestaBoolean = question.Type == QuestionType.TrueFalse ? question.IsTrue : null;
                string? respuestaOpcion = question.Type == QuestionType.MultipleChoice
                    ? GetCorrectOptionLetter(question)?.ToString()
                    : null;
                string respuestaModelo = question.CorrectAnswer ?? "";
                string explicacion = question.Explanation ?? "";
                string modeloIA = "gpt-4-turbo";
                decimal calidad = 0.85m;
                var ahora = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

                using var command = connection.CreateCommand();

                // 🆕 QUERY ACTUALIZADA CON source_chunks
                command.CommandText = @"
                INSERT INTO preguntas_generadas 
                (tipo, modalidad_id, tema_id, subtema_id, nivel, texto_pregunta, respuesta_correcta_boolean, 
                 respuesta_correcta_opcion, respuesta_modelo, explicacion, activa, creada_por, 
                 fecha_creacion, fecha_actualizacion, veces_utilizada, veces_correcta, modelo_ia, calidad_estimada,
                 source_chunks)
                VALUES 
                ($1::tipo_pregunta, $2, $3, $4, $5::nivel_dificultad, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15, $16, $17, $18, $19)
                RETURNING id";

                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = tipoPregunta });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = MODALIDAD_ESCRITO });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = temaId });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = (object?)subtemaId ?? DBNull.Value });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = nivelDificultad });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = question.QuestionText });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = (object?)respuestaBoolean ?? DBNull.Value });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = (object?)respuestaOpcion ?? DBNull.Value });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = respuestaModelo });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = explicacion });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = true });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = creadaPor });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = ahora });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = ahora });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 0 });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = 0 });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = modeloIA });
                command.Parameters.Add(new Npgsql.NpgsqlParameter { Value = calidad });

                // 🆕 PARÁMETRO 19: source_chunks
                command.Parameters.Add(new Npgsql.NpgsqlParameter
                {
                    Value = question.SourceChunkIds.Any()
                        ? question.SourceChunkIds.ToArray()
                        : (object)DBNull.Value,
                    NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text
                });

                var result = await command.ExecuteScalarAsync();
                var preguntaId = Convert.ToInt32(result);

                // Guardar opciones
                if (question.Type == QuestionType.MultipleChoice && question.Options != null)
                {
                    var opciones = new List<PreguntaOpcione>();
                    char[] letras = { 'A', 'B', 'C', 'D' };

                    for (int i = 0; i < Math.Min(question.Options.Count, 4); i++)
                    {
                        opciones.Add(new PreguntaOpcione
                        {
                            PreguntaGeneradaId = preguntaId,
                            Opcion = letras[i],
                            TextoOpcion = question.Options[i].Text,
                            EsCorrecta = question.Options[i].IsCorrect
                        });
                    }

                    _context.AddRange(opciones);
                    await _context.SaveChangesAsync();
                }

                savedIds.Add(preguntaId);

                _logger.LogInformation(
                    "Pregunta guardada: ID {Id}, Tipo: {Tipo}, Nivel: {Nivel}, Chunks: {ChunkCount}",
                    preguntaId, tipoPregunta, nivelDificultad, question.SourceChunkIds.Count);
            }

            return savedIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error guardando preguntas en base de datos");
            throw;
        }
    }

    // Método existente - mantener como está
    private char? ExtractCorrectOptionLetter(StudyQuestion question)
    {
        if (question.Options == null || !question.Options.Any())
            return null;

        var correctOption = question.Options.FirstOrDefault(o => o.IsCorrect);
        if (correctOption == null)
            return null;

        var index = question.Options.IndexOf(correctOption);
        return (char)('A' + index);
    }

    public async Task<int> GetOrCreateTemaId(string temaName, int areaId)
    {
        var tema = await _context.Temas
            .FirstOrDefaultAsync(t => t.Nombre == temaName && t.AreaId == areaId);

        if (tema == null)
        {
            tema = new Tema
            {
                Nombre = temaName,
                AreaId = areaId,
                Activo = true,
                FechaCreacion = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)
            };

            _context.Temas.Add(tema);
            await _context.SaveChangesAsync();
        }

        return tema.Id;
    }

    public async Task<int> GetAreaIdByName(string areaName)
    {
        var area = await _context.Areas
            .FirstOrDefaultAsync(a => a.Nombre.ToLower().Contains(areaName.ToLower()));

        if (area == null)
        {
            area = new Area
            {
                Nombre = areaName,
                Activo = true,
                FechaCreacion = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified)
            };

            _context.Areas.Add(area);
            await _context.SaveChangesAsync();
        }

        return area.Id;
    }

    // ═══════════════════════════════════════════════════════════════
    // MÉTODOS PRIVADOS (no están en la interfaz)
    // ═══════════════════════════════════════════════════════════════

    private char? GetCorrectOptionLetter(StudyQuestion question)
    {
        if (question.Options == null || !question.Options.Any())
            return null;

        var correctOption = question.Options.FirstOrDefault(o => o.IsCorrect);
        if (correctOption == null)
            return null;

        var index = question.Options.IndexOf(correctOption);
        return (char)('A' + index);
    }

    private async Task<int> GetOrCreateModalidadId(QuestionType questionType)
    {
        string nombreModalidad = "escrito";

        var modalidad = await _context.ModalidadesTest
            .FirstOrDefaultAsync(m => m.Nombre == nombreModalidad);

        if (modalidad == null)
        {
            modalidad = new ModalidadTest
            {
                Nombre = nombreModalidad,
                Descripcion = "Preguntas de modalidad escrita"
            };

            _context.ModalidadesTest.Add(modalidad);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Modalidad creada: {Nombre} con ID {Id}",
                nombreModalidad, modalidad.Id);
        }

        return modalidad.Id;
    }
}