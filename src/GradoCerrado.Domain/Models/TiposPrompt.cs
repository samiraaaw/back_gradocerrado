using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class TiposPrompt
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public bool? Activo { get; set; }

    public virtual ICollection<PromptsSistema> PromptsSistemas { get; set; } = new List<PromptsSistema>();
}
