// 📁 src/GradoCerrado.Domain/Models/EstudianteNotificacionConfig.cs
namespace GradoCerrado.Domain.Models;

public partial class EstudianteNotificacionConfig
{
    public int EstudianteId { get; set; } // ✅ Esta es la clave primaria
    public bool NotificacionesHabilitadas { get; set; } = true;
    public string? TokenDispositivo { get; set; }
    public DateTime? FechaInicio { get; set; }
    public DateTime? FechaFin { get; set; }
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    // Relación
    public virtual Estudiante Estudiante { get; set; } = null!;
}