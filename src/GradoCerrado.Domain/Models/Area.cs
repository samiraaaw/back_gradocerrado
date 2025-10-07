using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class Area
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public string? Icono { get; set; }

    public double? Importancia { get; set; }

    public bool? Activo { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public virtual ICollection<FragmentosQdrant> FragmentosQdrants { get; set; } = new List<FragmentosQdrant>();

    public virtual ICollection<Tema> Temas { get; set; } = new List<Tema>();

    public virtual ICollection<Test> Tests { get; set; } = new List<Test>();
}
