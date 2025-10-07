// src/GradoCerrado.Application/Interfaces/ITestRepository.cs
using GradoCerrado.Domain.Models;

namespace GradoCerrado.Application.Interfaces;

public interface ITestRepository
{
    Task<Test?> GetByIdAsync(int id);
    Task<TestPregunta?> GetTestPreguntaAsync(int testId, short numeroOrden);
    Task<TestPregunta> CreateTestPreguntaAsync(TestPregunta testPregunta);
    Task UpdateTestPreguntaAsync(TestPregunta testPregunta);
    Task<List<TestPregunta>> GetTestPreguntasByTestIdAsync(int testId);
    System.Data.Common.DbConnection GetDbConnection();
}