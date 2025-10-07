// src/GradoCerrado.Application/Interfaces/IPreguntaRepository.cs
using GradoCerrado.Domain.Models;

namespace GradoCerrado.Application.Interfaces;

public interface IPreguntaRepository
{
    Task<PreguntasGenerada?> GetByIdAsync(int id);
    Task UpdateAsync(PreguntasGenerada pregunta);
}