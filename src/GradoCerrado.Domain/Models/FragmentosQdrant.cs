using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class FragmentosQdrant
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public string Title { get; set; } = null!;

    public string ChunkId { get; set; } = null!;

    public string Content { get; set; } = null!;

    public int? AreaId { get; set; }

    public int? TemaId { get; set; }

    public int? UsadoEnPreguntas { get; set; }

    public bool? Activo { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Area? Area { get; set; }

    public virtual Tema? Tema { get; set; }
}
