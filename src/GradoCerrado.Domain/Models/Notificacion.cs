using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class Notificacion
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    public int TiposNotificacionId { get; set; }

    public string Titulo { get; set; } = null!;

    public string Mensaje { get; set; } = null!;

    public string? DatosAdicionales { get; set; }

    public DateTime FechaProgramada { get; set; }

    public bool? Enviado { get; set; }

    public DateTime? FechaEnviado { get; set; }

    public bool? Leido { get; set; }

    public DateTime? FechaLeido { get; set; }

    public bool? AccionTomada { get; set; }

    public DateTime? FechaAccion { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public virtual Estudiante Estudiante { get; set; } = null!;

    public virtual TiposNotificacion TiposNotificacion { get; set; } = null!;
}
