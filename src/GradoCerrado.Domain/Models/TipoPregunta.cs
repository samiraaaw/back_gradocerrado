// src/GradoCerrado.Domain/Models/TipoPregunta.cs
namespace GradoCerrado.Domain.Models;

/// <summary>
/// Enum que coincide con tipo_pregunta de PostgreSQL
/// </summary>
public enum TipoPregunta
{
    verdadero_falso,
    seleccion_multiple,
    desarrollo
}