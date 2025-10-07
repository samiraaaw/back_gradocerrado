using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class Test
{
    public int Id { get; set; }

    public int EstudianteId { get; set; }

    public int? ModalidadId { get; set; }

    public int TipoTestId { get; set; }

    public int? AreaId { get; set; }

    public int NumeroPreguntasTotal { get; set; }

    public int? TiempoLimiteMinutos { get; set; }

    public int? NumeroPreguntasBasico { get; set; }

    public int? NumeroPreguntasIntermedio { get; set; }

    public int? NumeroPreguntasAvanzado { get; set; }

    public decimal? PuntajeObtenido { get; set; }

    public decimal? PuntajeMaximo { get; set; }

    public decimal? PorcentajeAcierto { get; set; }

    public DateTime? HoraInicio { get; set; }

    public DateTime? HoraFin { get; set; }

    public int? DuracionSegundos { get; set; }

    public int? DuracionEstimada { get; set; }

    public bool? Completado { get; set; }

    public string? NotasAdicionales { get; set; }

    public DateTime? FechaCreacion { get; set; }

    public virtual Area? Area { get; set; }

    public virtual Estudiante Estudiante { get; set; } = null!;

    public virtual ModalidadTest? Modalidad { get; set; }

    public virtual ICollection<TestPregunta> TestPregunta { get; set; } = new List<TestPregunta>();

    public virtual TiposTest TipoTest { get; set; } = null!;
}
