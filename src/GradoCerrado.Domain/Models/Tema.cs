using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class Tema
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public int AreaId { get; set; }

    public string? Descripcion { get; set; }

    public bool? Activo { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public string? NombreNorm { get; set; }

    public virtual Area Area { get; set; } = null!;

    public virtual ICollection<FragmentosQdrant> FragmentosQdrants { get; set; } = new List<FragmentosQdrant>();

    public virtual ICollection<PreguntasGenerada> PreguntasGenerada { get; set; } = new List<PreguntasGenerada>();

    public virtual ICollection<Subtema> Subtemas { get; set; } = new List<Subtema>();
    
}
