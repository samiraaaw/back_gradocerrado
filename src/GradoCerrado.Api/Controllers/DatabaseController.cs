using Microsoft.AspNetCore.Mvc;
using GradoCerrado.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace GradoCerrado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DatabaseController : ControllerBase
{
    private readonly GradocerradoContext _context;

    public DatabaseController(GradocerradoContext context)
    {
        _context = context;
    }

    [HttpGet("status")]
    public async Task<ActionResult> GetDatabaseStatus()
    {
        try
        {
            // Verificar conexión
            var canConnect = await _context.Database.CanConnectAsync();

            if (!canConnect)
            {
                return BadRequest(new
                {
                    status = "CONNECTION_FAILED",
                    message = "No se puede conectar a la base de datos",
                    timestamp = DateTime.Now
                });
            }

            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            // Contar tablas
            var tableCommand = connection.CreateCommand();
            tableCommand.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'preguntas'";
            var tableCount = Convert.ToInt32(await tableCommand.ExecuteScalarAsync());

            await connection.CloseAsync();

            return Ok(new
            {
                status = "SUCCESS",
                connection = "OK",
                total_tables = tableCount,
                is_setup_needed = tableCount == 0,
                recommendation = tableCount == 0 ?
                    "Ejecutar /create-tables para configurar" :
                    "Base de datos configurada",
                timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                status = "ERROR",
                message = "Error verificando el estado de la base de datos",
                error = ex.Message,
                timestamp = DateTime.Now
            });
        }
    }

    [HttpGet("check-and-setup")]
    public async Task<ActionResult> CheckAndSetupDatabase()
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            // 1. Verificar si hay tablas
            var command = connection.CreateCommand();
            command.CommandText = "SELECT table_name FROM information_schema.tables WHERE table_schema = 'public' ORDER BY table_name";

            var existingTables = new List<string>();
            using (var reader = await command.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    existingTables.Add(reader.GetString(0));
                }
            }

            var result = new
            {
                existing_tables_count = existingTables.Count,
                existing_tables = existingTables,
                needs_setup = existingTables.Count == 0,
                message = existingTables.Count == 0 ?
                    "Base de datos vacía - necesita configuración inicial" :
                    $"Base de datos ya configurada con {existingTables.Count} tablas"
            };

            await connection.CloseAsync();

            return Ok(new
            {
                status = "SUCCESS",
                database_status = result,
                timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                status = "ERROR",
                message = "Error verificando la base de datos",
                error = ex.Message,
                timestamp = DateTime.Now
            });
        }
    }

    [HttpPost("create-tables")]
    public async Task<ActionResult> CreateTables()
    {
        try
        {
            await _context.Database.EnsureCreatedAsync();

            // Verificar que se crearon
            var connection = _context.Database.GetDbConnection();
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'";
            var tableCount = Convert.ToInt32(await command.ExecuteScalarAsync());

            await connection.CloseAsync();

            return Ok(new
            {
                status = "SUCCESS",
                message = $"Tablas creadas exitosamente. Total: {tableCount} tablas",
                tables_created = tableCount,
                timestamp = DateTime.Now
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                status = "ERROR",
                message = "Error creando las tablas",
                error = ex.Message,
                timestamp = DateTime.Now
            });
        }
    }
    [HttpPost("execute-sql")]
public async Task<ActionResult> ExecuteSQL([FromBody] SQLRequest request)
{
    try
    {
        await _context.Database.ExecuteSqlRawAsync(request.SQL);
        
        return Ok(new 
        { 
            status = "SUCCESS",
            message = "SQL ejecutado exitosamente",
            timestamp = DateTime.Now
        });
    }
    catch (Exception ex)
    {
        return StatusCode(500, new 
        { 
            status = "ERROR",
            message = "Error ejecutando SQL",
            error = ex.Message,
            timestamp = DateTime.Now
        });
    }
}

public class SQLRequest
{
    public string SQL { get; set; } = string.Empty;
}
}