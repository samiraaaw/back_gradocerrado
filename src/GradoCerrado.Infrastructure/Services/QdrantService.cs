using GradoCerrado.Application.Interfaces;
using GradoCerrado.Infrastructure.Configuration;
using GradoCerrado.Infrastructure.DTOs;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Text;
using System.Text.Json;

namespace GradoCerrado.Infrastructure.Services;

public class QdrantService : IVectorService
{
    private readonly QdrantClient _qdrantClient;
    private readonly HttpClient _httpClient;
    private readonly QdrantSettings _settings;
    private readonly IEmbeddingService _embeddingService;
    private readonly string _baseUrl;
    private readonly string _collectionName;
    private readonly ILogger<QdrantService> _logger;

    public QdrantService(
        IOptions<QdrantSettings> settings,
        HttpClient httpClient,
        IEmbeddingService embeddingService,
        ILogger<QdrantService> logger)
    {
        _settings = settings.Value;
        _httpClient = httpClient;
        _embeddingService = embeddingService;
        _logger = logger;
        _baseUrl = _settings.Url.TrimEnd('/');
        _collectionName = _settings.CollectionName;

        // ✅ INICIALIZAR QdrantClient
        _qdrantClient = new QdrantClient(
            host: ExtractHost(_settings.Url),
            port: ExtractPort(_settings.Url),
            https: _settings.Url.StartsWith("https"),
            apiKey: _settings.ApiKey
        );

        // Configurar headers para HTTP requests
        if (!string.IsNullOrEmpty(_settings.ApiKey))
        {
            _httpClient.DefaultRequestHeaders.Add("api-key", _settings.ApiKey);
        }

        _logger.LogInformation("✅ QdrantService inicializado - Collection: {Collection}", _collectionName);
    }

    // ============================================
    // MÉTODOS PÚBLICOS
    // ============================================
    public async Task<CollectionStats> GetCollectionStatsAsync()
    {
        try
        {
            // Usar HTTP REST API en lugar de gRPC para mayor compatibilidad
            var response = await _httpClient.GetAsync($"{_baseUrl}/collections/{_collectionName}");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var collectionResponse = JsonSerializer.Deserialize<QdrantCollectionInfoResponse>(content);

                return new CollectionStats
                {
                    VectorsCount = (long)(collectionResponse?.result?.vectors_count ?? 0),
                    IndexedVectorsCount = (long)(collectionResponse?.result?.indexed_vectors_count ?? 0),
                    Status = collectionResponse?.result?.status ?? "unknown"
                };
            }
            else
            {
                _logger.LogWarning("Error obteniendo estadísticas: {StatusCode}", response.StatusCode);
                return new CollectionStats
                {
                    VectorsCount = 0,
                    IndexedVectorsCount = 0,
                    Status = "error"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estadísticas de Qdrant");
            throw;
        }
    }

    // Agregar a tu archivo DTOs (probablemente Infrastructure/DTOs/QdrantDTOs.cs)
    public class QdrantCollectionInfoResponse
    {
        public QdrantCollectionInfo? result { get; set; }
    }

    public class QdrantCollectionInfo
    {
        public ulong vectors_count { get; set; }
        public ulong indexed_vectors_count { get; set; }
        public string status { get; set; } = "unknown";
    }
    public async Task<bool> CollectionExistsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/collections");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var collectionsResponse = JsonSerializer.Deserialize<QdrantCollectionsResponse>(content);
                return collectionsResponse?.result?.collections?.Any(c => c.name == _collectionName) ?? false;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error verificando colección");
            return false;
        }
    }

    public async Task<bool> InitializeCollectionAsync()
    {
        try
        {
            // Verificar si la colección ya existe
            if (await CollectionExistsAsync())
            {
                _logger.LogInformation("La colección '{Collection}' ya existe", _collectionName);
                return true;
            }

            // Crear la colección
            var createRequest = new
            {
                vectors = new
                {
                    size = _settings.VectorSize,
                    distance = _settings.Distance
                }
            };

            var json = JsonSerializer.Serialize(createRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(
                $"{_baseUrl}/collections/{_collectionName}",
                content
            );

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Colección '{Collection}' creada exitosamente", _collectionName);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Error creando colección: {Error}", errorContent);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creando colección");
            return false;
        }
    }

    public async Task<string> AddDocumentAsync(string content, Dictionary<string, object> metadata)
    {
        try
        {
            var documentId = Guid.NewGuid().ToString();

            // Generar embedding real del contenido usando OpenAI
            var vector = await _embeddingService.GenerateEmbeddingAsync(content);

            // Preparar payload
            var payload = new Dictionary<string, object>(metadata)
            {
                ["content"] = content
            };

            var point = new
            {
                id = documentId,
                vector = vector,
                payload = payload
            };

            var upsertRequest = new
            {
                points = new[] { point }
            };

            var json = JsonSerializer.Serialize(upsertRequest);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PutAsync(
                $"{_baseUrl}/collections/{_collectionName}/points",
                httpContent
            );

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Documento agregado con embedding real: {DocumentId}", documentId);
                return documentId;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                throw new Exception($"Error agregando documento: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error agregando documento");
            throw;
        }
    }

    public async Task<List<SearchResult>> SearchSimilarAsync(string query, int limit = 5)
    {
        try
        {
            // Validación: Manejar query vacía
            if (string.IsNullOrWhiteSpace(query))
            {
                query = "documento legal";
            }

            // Generar embedding real de la consulta usando OpenAI
            var queryVector = await _embeddingService.GenerateEmbeddingAsync(query);

            var searchRequest = new
            {
                vector = queryVector,
                limit = limit,
                with_payload = true
            };

            var json = JsonSerializer.Serialize(searchRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/collections/{_collectionName}/points/search",
                content
            );

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var searchResponse = JsonSerializer.Deserialize<QdrantSearchResponse>(responseContent);

                var results = searchResponse?.result?.Select(hit => new SearchResult
                {
                    Id = hit.id?.ToString() ?? "",
                    Content = hit.payload?.GetValueOrDefault("content")?.ToString() ?? "",
                    Score = hit.score,
                    Metadata = hit.payload ?? new Dictionary<string, object>()
                }).ToList() ?? new List<SearchResult>();

                _logger.LogInformation("Búsqueda semántica completada para: '{Query}', resultados: {Count}",
                    query, results.Count);
                return results;
            }
            else
            {
                _logger.LogWarning("Error en búsqueda: {StatusCode}", response.StatusCode);
                return new List<SearchResult>();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en búsqueda");
            return new List<SearchResult>();
        }
    }

    public async Task<bool> DeleteDocumentAsync(string documentId)
    {
        try
        {
            var deleteRequest = new
            {
                points = new[] { documentId }
            };

            var json = JsonSerializer.Serialize(deleteRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(
                $"{_baseUrl}/collections/{_collectionName}/points/delete",
                content
            );

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Documento eliminado: {DocumentId}", documentId);
                return true;
            }
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error eliminando documento");
            return false;
        }
    }

    // ============================================
    // MÉTODOS PRIVADOS HELPER
    // ============================================

    private string ExtractHost(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            _logger.LogWarning("No se pudo extraer host de {Url}, usando URL completa", url);
            return url;
        }
    }

    private int ExtractPort(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Port > 0 ? uri.Port : (uri.Scheme == "https" ? 443 : 6333);
        }
        catch
        {
            return 6333; // Puerto por defecto de Qdrant
        }
    }
}