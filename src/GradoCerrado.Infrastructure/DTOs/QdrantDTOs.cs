namespace GradoCerrado.Infrastructure.DTOs;

public class QdrantCollectionsResponse
{
    public QdrantCollectionsResult? result { get; set; }
}

public class QdrantCollectionsResult
{
    public List<QdrantCollection>? collections { get; set; }
}

public class QdrantCollection
{
    public string name { get; set; } = string.Empty;
}

public class QdrantSearchResponse
{
    public List<QdrantSearchHit>? result { get; set; }
}

public class QdrantSearchHit
{
    public object? id { get; set; }
    public double score { get; set; }
    public Dictionary<string, object>? payload { get; set; }
}