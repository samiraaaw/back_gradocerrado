namespace GradoCerrado.Domain.Entities;

public class Student
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public DifficultyLevel CurrentLevel { get; set; } = DifficultyLevel.Basic;
    public DateTime RegistrationDate { get; set; } = DateTime.UtcNow;
    public DateTime LastAccess { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // Relaciones
    public List<UserStudySession> StudySessions { get; set; } = new();
}