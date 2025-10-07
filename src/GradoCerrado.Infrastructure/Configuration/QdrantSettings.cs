namespace GradoCerrado.Infrastructure.Configuration;

public class QdrantSettings
{
    public const string SectionName = "Qdrant";
    
    public string Url { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string CollectionName { get; set; } = "legal_documents";
    public int VectorSize { get; set; } = 1536; // Tamaño para OpenAI text-embedding-ada-002
    public string Distance { get; set; } = "Cosine";
}