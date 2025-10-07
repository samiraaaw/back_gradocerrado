using GradoCerrado.Domain.Entities;

namespace GradoCerrado.Application.Interfaces;

/// <summary>
/// Servicio para persistir preguntas generadas en la base de datos
/// </summary>
public interface IQuestionPersistenceService
{
    /// <summary>
    /// Guarda una lista de preguntas en la base de datos
    /// </summary>
    /// <param name="studyQuestions">Lista de preguntas a guardar</param>
    /// <param name="temaId">ID del tema asociado</param>
    /// <param name="subtemaId">ID del subtema (opcional)</param>
    /// <param name="modalidadId">ID de la modalidad (1=escrito, 2=oral)</param>
    /// <param name="creadaPor">Identificador de quién creó las preguntas</param>
    /// <returns>Lista de IDs de las preguntas guardadas</returns>
    Task<List<int>> SaveQuestionsToDatabase(
        List<StudyQuestion> studyQuestions,
        int temaId,
        int? subtemaId = null,
        int modalidadId = 1,
        string creadaPor = "AI");

    /// <summary>
    /// Obtiene o crea un tema en la base de datos
    /// </summary>
    /// <param name="temaName">Nombre del tema</param>
    /// <param name="areaId">ID del área legal</param>
    /// <returns>ID del tema</returns>
    Task<int> GetOrCreateTemaId(string temaName, int areaId);

    /// <summary>
    /// Obtiene el ID de un área legal por nombre
    /// </summary>
    /// <param name="areaName">Nombre del área legal</param>
    /// <returns>ID del área</returns>
    Task<int> GetAreaIdByName(string areaName);
}