
// 📁 src/GradoCerrado.Domain/Models/EstudianteConfiguracion.cs
namespace GradoCerrado.Domain.Models;

public partial class EstudianteConfiguracion
{
    public int EstudianteId { get; set; } // ✅ Esta es la clave primaria
    public int ObjetivoPreguntasDiarias { get; set; } = 10;
    public bool RecordatoriosActivos { get; set; } = true;
    public TimeOnly HorarioEstudioPreferido { get; set; } = new TimeOnly(19, 0);
    public DateTime FechaActualizacion { get; set; } = DateTime.UtcNow;

    // Relación
    public virtual Estudiante Estudiante { get; set; } = null!;
}
