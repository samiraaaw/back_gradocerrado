using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class Subtema
{
    public int Id { get; set; }
    
    public int TemaId { get; set; }
    
    public string Nombre { get; set; } = null!;
    
    public string? Descripcion { get; set; }
    
    public int Orden { get; set; }
    
    public bool? Activo { get; set; }
    
    public DateTime? FechaCreacion { get; set; }
    
    public virtual Tema Tema { get; set; } = null!;
    
    public virtual ICollection<PreguntasGenerada> PreguntasGenerada { get; set; } = new List<PreguntasGenerada>();
}