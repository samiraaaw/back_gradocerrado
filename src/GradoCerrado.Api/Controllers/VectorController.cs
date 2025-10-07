using GradoCerrado.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace GradoCerrado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class VectorController : ControllerBase
{
    private readonly IVectorService _vectorService;
    private readonly ILogger<VectorController> _logger;

    public VectorController(IVectorService vectorService, ILogger<VectorController> logger)
    {
        _vectorService = vectorService;
        _logger = logger;
    }

    [HttpPost("initialize")]
    public async Task<ActionResult> InitializeCollection()
    {
        try
        {
            var result = await _vectorService.InitializeCollectionAsync();
            
            if (result)
            {
                return Ok(new { message = "Colección inicializada correctamente", success = true });
            }
            
            return BadRequest(new { message = "Error al inicializar la colección", success = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al inicializar colección de Qdrant");
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    [HttpGet("status")]
    public async Task<ActionResult> GetCollectionStatus()
    {
        try
        {
            var exists = await _vectorService.CollectionExistsAsync();
            
            return Ok(new { 
                collectionExists = exists,
                status = exists ? "Colección disponible" : "Colección no encontrada"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al verificar estado de colección");
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    [HttpPost("document")]
    public async Task<ActionResult> AddDocument([FromBody] AddDocumentRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return BadRequest("El contenido no puede estar vacío");
            }

            var metadata = new Dictionary<string, object>
            {
                ["title"] = request.Title ?? "Sin título",
                ["category"] = request.Category ?? "general",
                ["created_at"] = DateTime.UtcNow.ToString("O")
            };

            var documentId = await _vectorService.AddDocumentAsync(request.Content, metadata);
            
            return Ok(new { 
                documentId = documentId,
                message = "Documento agregado correctamente",
                success = true
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al agregar documento");
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    [HttpPost("search")]
    public async Task<ActionResult> SearchDocuments([FromBody] SearchRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest("La consulta no puede estar vacía");
            }

            var results = await _vectorService.SearchSimilarAsync(request.Query, request.Limit);
            
            return Ok(new { 
                results = results,
                count = results.Count,
                query = request.Query
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al buscar documentos");
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }

    [HttpDelete("document/{documentId}")]
    public async Task<ActionResult> DeleteDocument(string documentId)
    {
        try
        {
            var result = await _vectorService.DeleteDocumentAsync(documentId);
            
            if (result)
            {
                return Ok(new { message = "Documento eliminado correctamente", success = true });
            }
            
            return NotFound(new { message = "Documento no encontrado", success = false });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar documento");
            return StatusCode(500, new { message = "Error interno del servidor", error = ex.Message });
        }
    }
}

public class AddDocumentRequest
{
    public string Content { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? Category { get; set; }
}

public class SearchRequest
{
    public string Query { get; set; } = string.Empty;
    public int Limit { get; set; } = 5;
}