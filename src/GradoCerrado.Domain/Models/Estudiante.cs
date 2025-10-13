using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class Estudiante
{
    public int Id { get; set; }
    public string Nombre { get; set; } = null!;
    public string? SegundoNombre { get; set; }
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string? NombreCompleto { get; set; }
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string? FotoPerfil { get; set; }
    public string? IdAvatarSeleccionado { get; set; }
    public bool ModoAdaptativoActivo { get; set; } = false;
    public DateTime? FechaModificacion { get; set; }

    // ✅ Recordatorios

    public int? FrecuenciaEstudioSemanal { get; set; }
    public string? ObjetivoDiasEstudio { get; set; }
    public string? DiasPreferidosEstudio { get; set; }
    public bool? RecordatorioEstudioActivo { get; set; }
    public TimeOnly? HoraRecordatorio { get; set; }

    // Columnas adicionales que tienes en tu BD
    public string? NivelActual { get; set; }
    public string? NivelDiagnosticado { get; set; }

    public bool? TestDiagnosticoCompletado { get; set; }
    public DateTime? FechaTestDiagnostico { get; set; }
    public DateTime? FechaRegistro { get; set; }
    public DateTime? UltimoAcceso { get; set; }
    public bool? Activo { get; set; }
    public bool? Verificado { get; set; }

    // Relaciones
    public virtual EstudianteNotificacionConfig? EstudianteNotificacionConfig { get; set; }
    public virtual ICollection<Notificacion> Notificaciones { get; set; } = new List<Notificacion>();
    public virtual ICollection<Test> Tests { get; set; } = new List<Test>();
}