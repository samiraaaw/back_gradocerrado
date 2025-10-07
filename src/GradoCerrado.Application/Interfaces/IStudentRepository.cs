// src/GradoCerrado.Application/Interfaces/IStudentRepository.cs
using GradoCerrado.Domain.Entities; // ← ESTA LÍNEA FALTABA

namespace GradoCerrado.Application.Interfaces;

public interface IStudentRepository
{
    Task<Student?> GetByIdAsync(Guid id);
    Task<Student?> GetByEmailAsync(string email);
    Task<Student> CreateAsync(Student student);
    Task UpdateAsync(Student student);
    Task<bool> ExistsAsync(string email);
}
