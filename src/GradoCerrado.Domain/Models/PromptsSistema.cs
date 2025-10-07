using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class PromptsSistema
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public int TipoPromptId { get; set; }

    public string Plantilla { get; set; } = null!;

    public int? Version { get; set; }

    public bool? Activo { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public string? UsuarioCreacion { get; set; }

    public string? UsuarioActualizacion { get; set; }

    public DateTime? FechaActualizacion { get; set; }

    public virtual ICollection<PreguntasGenerada> PreguntasGenerada { get; set; } = new List<PreguntasGenerada>();

    public virtual TiposPrompt TipoPrompt { get; set; } = null!;
}
