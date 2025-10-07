using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class PreguntaOpcione
{
    public int Id { get; set; }

    public int PreguntaGeneradaId { get; set; }

    public char Opcion { get; set; }

    public string TextoOpcion { get; set; } = null!;

    public bool? EsCorrecta { get; set; }

    public virtual PreguntasGenerada PreguntaGenerada { get; set; } = null!;
}
