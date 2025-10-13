// src/GradoCerrado.Application/Interfaces/IContentClassifierService.cs

namespace GradoCerrado.Application.Interfaces;

/// <summary>
/// Servicio para clasificar contenido en temas y subtemas existentes
/// </summary>
public interface IContentClassifierService
{
    /// <summary>
    /// Clasifica el contenido del documento en temas y subtemas existentes en la BD
    /// </summary>
    /// <param name="content">Contenido del documento a clasificar</param>
    /// <param name="areaId">ID del área legal</param>
    /// <returns>Lista de clasificaciones (tema_id, subtema_id opcional)</returns>
    Task<List<ContentClassification>> ClassifyContentAsync(string content, int areaId);

    /// <summary>
    /// Obtiene todos los temas y subtemas disponibles para un área
    /// </summary>
    Task<List<TemaConSubtemas>> GetAvailableTemasAsync(int areaId);
}

/// <summary>
/// Resultado de clasificación de contenido
/// </summary>
public class ContentClassification
{
    public int TemaId { get; set; }
    public string TemaNombre { get; set; } = string.Empty;
    public int? SubtemaId { get; set; }
    public string? SubtemaNombre { get; set; }
    public double Confidence { get; set; }
}

/// <summary>
/// Tema con sus subtemas asociados
/// </summary>
public class TemaConSubtemas
{
    public int TemaId { get; set; }
    public string TemaNombre { get; set; } = string.Empty;
    public List<SubtemaInfo> Subtemas { get; set; } = new();
}

/// <summary>
/// Información de un subtema
/// </summary>
public class SubtemaInfo
{
    public int SubtemaId { get; set; }
    public string SubtemaNombre { get; set; } = string.Empty;
}