using System;
using System.Collections.Generic;

namespace GradoCerrado.Domain.Models;

public partial class MetricasEstudiante
{
    public int EstudianteId { get; set; }

    public int? RachaDiasActual { get; set; }

    public int? RachaDiasMaxima { get; set; }

    public DateOnly? UltimoDiaEstudio { get; set; }

    public DateTime? FechaActualizacion { get; set; }

    public int? VersionCalculo { get; set; }

    public int? TotalDiasEstudiados { get; set; }

    public DateOnly? PrimeraFechaEstudio { get; set; }

    public decimal? PromedioPreguntasDia { get; set; }

    public decimal? PromedioAciertos { get; set; }
}
