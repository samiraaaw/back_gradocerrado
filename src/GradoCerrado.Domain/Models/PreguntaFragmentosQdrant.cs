using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class PreguntaFragmentosQdrant
{
    public int Id { get; set; }

    public int PreguntaGeneradaId { get; set; }

    public string ChunkId { get; set; } = null!;

    public decimal? Relevancia { get; set; }

    public short? OrdenUso { get; set; }

    public virtual PreguntasGenerada PreguntaGenerada { get; set; } = null!;
}
