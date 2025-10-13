using GradoCerrado.Application.Interfaces;
using GradoCerrado.Domain.Entities;
using GradoCerrado.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;
using System.Data;

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

    public async Task<List<int>> SaveQuestionsToDatabase(
        List<StudyQuestion> studyQuestions,
        int temaId,
        int? subtemaId = null,
        int modalidadId = 1,
        string creadaPor = "AI")
    {
        try
        {
            var savedIds = new List<int>();

            foreach (var question in studyQuestions)
            {
                // CORRECCIÓN: Usar valores del enum con snake_case
                var tipoPregunta = question.Type == QuestionType.MultipleChoice
                    ? TipoPregunta.seleccion_multiple
                    : TipoPregunta.verdadero_falso;

                var nivelDificultad = question.Difficulty switch
                {
                    DifficultyLevel.Basic => 1,
                    DifficultyLevel.Intermediate => 2,
                    DifficultyLevel.Advanced => 3,
                    _ => 2
                };

                var connection = _context.Database.GetDbConnection();
                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using var command = connection.CreateCommand();

                command.CommandText = @"
                    INSERT INTO preguntas_generadas (
                        tema_id, subtema_id, modalidad_id, tipo_pregunta,
                        texto_pregunta, respuesta_correcta, respuesta_correcta_boolean,
                        explicacion, nivel_dificultad, activa, creada_por,
                        fecha_creacion, veces_utilizada, veces_correcta, contexto_fragmentos
                    ) VALUES (
                        $1, $2, $3, $4, $5, $6, $7, $8, $9, $10, $11, $12, $13, $14, $15
                    ) RETURNING id";

                command.Parameters.Add(new NpgsqlParameter { Value = temaId });
                command.Parameters.Add(new NpgsqlParameter { Value = (object?)subtemaId ?? DBNull.Value });
                command.Parameters.Add(new NpgsqlParameter { Value = modalidadId });
                command.Parameters.Add(new NpgsqlParameter
                {
                    Value = tipoPregunta.ToString(),
                    NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Unknown
                });
                command.Parameters.Add(new NpgsqlParameter { Value = question.QuestionText });
                command.Parameters.Add(new NpgsqlParameter { Value = (object?)GetCorrectOptionLetter(question) ?? DBNull.Value });
                command.Parameters.Add(new NpgsqlParameter { Value = (object?)question.IsTrue ?? DBNull.Value });
                command.Parameters.Add(new NpgsqlParameter { Value = question.Explanation ?? "Sin explicación" });
                command.Parameters.Add(new NpgsqlParameter { Value = nivelDificultad });
                command.Parameters.Add(new NpgsqlParameter { Value = true });
                command.Parameters.Add(new NpgsqlParameter { Value = creadaPor });
                command.Parameters.Add(new NpgsqlParameter { Value = DateTime.UtcNow });
                command.Parameters.Add(new NpgsqlParameter { Value = 0 });
                command.Parameters.Add(new NpgsqlParameter { Value = 0 });
                command.Parameters.Add(new NpgsqlParameter
                {
                    Value = question.SourceChunkIds != null && question.SourceChunkIds.Any()
                        ? question.SourceChunkIds.ToArray()
                        : (object)DBNull.Value,
                    NpgsqlDbType = NpgsqlTypes.NpgsqlDbType.Array | NpgsqlTypes.NpgsqlDbType.Text
                });

                var result = await command.ExecuteScalarAsync();
                var preguntaId = Convert.ToInt32(result);

                // Guardar opciones - SIEMPRE 3 OPCIONES (A, B, C)
                if (question.Type == QuestionType.MultipleChoice && question.Options != null)
                {
                    var opciones = new List<PreguntaOpcione>();
                    char[] letras = { 'A', 'B', 'C' }; // CAMBIO: Solo 3 letras

                    // Tomar solo las primeras 3 opciones
                    for (int i = 0; i < Math.Min(question.Options.Count, 3); i++)
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
                    preguntaId, tipoPregunta, nivelDificultad, question.SourceChunkIds?.Count ?? 0);
            }

            return savedIds;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error guardando preguntas en base de datos");
            throw;
        }
    }

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