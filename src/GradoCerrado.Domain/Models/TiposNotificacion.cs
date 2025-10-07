using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class TiposNotificacion
{
    public int Id { get; set; }

    public string Nombre { get; set; } = null!;

    public string? Descripcion { get; set; }

    public virtual ICollection<Notificacion> Notificaciones { get; set; } = new List<Notificacion>();
}
