using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class TestPregunta
{
    public int Id { get; set; }

    public int TestId { get; set; }

    public int? PreguntaGeneradaId { get; set; }

    public string? RespuestaTexto { get; set; }

    public bool? RespuestaBoolean { get; set; }

    public char? RespuestaOpcion { get; set; }

    public bool? EsCorrecta { get; set; }

    public int? TiempoRespuestaSegundos { get; set; }

    public DateTime? FechaRespuesta { get; set; }

    public short NumeroOrden { get; set; }

    public virtual PreguntasGenerada? PreguntaGenerada { get; set; }

    public virtual Test Test { get; set; } = null!;
}
