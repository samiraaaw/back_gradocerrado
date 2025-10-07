// src/GradoCerrado.Domain/Enums/Enums.cs - VERSIÓN UNIFICADA
namespace GradoCerrado.Domain.Entities;

public enum LegalDocumentType
{
    Law,              // Ley
    Decree,           // Decreto
    Jurisprudence,    // Jurisprudencia
    Doctrine,         // Doctrina
    Constitution,     // Constitución
    Code,             // Código
    StudyMaterial,    // Material de estudio
    CaseStudy,        // Caso de estudio
    Regulation        // Reglamento
}

public enum DifficultyLevel
{
    Basic,         // Básico
    Intermediate,  // Intermedio
    Advanced       // Avanzado
}

public enum QuestionType
{
    MultipleChoice,  // Selección múltiple
    TrueFalse,       // Verdadero/Falso
    OpenEnded // ?? NECESITAS AGREGAR ESTE PARA PREGUNTAS ORALES
}