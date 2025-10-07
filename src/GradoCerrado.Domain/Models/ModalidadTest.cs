using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class ModalidadTest
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public virtual ICollection<PreguntasGenerada> PreguntasGenerada { get; set; } = new List<PreguntasGenerada>();

    public virtual ICollection<Test> Tests { get; set; } = new List<Test>();
}
