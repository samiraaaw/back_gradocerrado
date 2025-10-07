using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GradoCerrado.Domain.Models;
using System.Security.Cryptography;
using System.Text;

namespace GradoCerrado.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly GradocerradoContext _context;
    private readonly ILogger<AuthController> _logger;

    public AuthController(GradocerradoContext context, ILogger<AuthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    [HttpGet("test-connection")]
    public async Task<ActionResult> TestConnection()
    {
        try
        {
            var count = await _context.Estudiantes.CountAsync();
            var dbName = _context.Database.GetDbConnection().Database;

            return Ok(new
            {
                success = true,
                database = dbName,
                estudiantesCount = count,
                message = "Conexión exitosa"
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [HttpGet("connection-info")]
    public async Task<ActionResult> GetConnectionInfo()
    {
        try
        {
            var connection = _context.Database.GetDbConnection();
            var connectionString = connection.ConnectionString;

            // Parsear la cadena para extraer los componentes
            var parts = connectionString.Split(';')
                .Where(part => !string.IsNullOrEmpty(part))
                .Select(part => part.Split('='))
                .Where(split => split.Length == 2)
                .ToDictionary(split => split[0], split => split[1]);

            return Ok(new
            {
                host = parts.GetValueOrDefault("Host", "N/A"),
                port = parts.GetValueOrDefault("Port", "N/A"),
                database = parts.GetValueOrDefault("Database", "N/A"),
                username = parts.GetValueOrDefault("Username", "N/A"),
                // NO mostrar password por seguridad
                hasPassword = parts.ContainsKey("Password")
            });
        }
        catch (Exception ex)
        {
            return Ok(new { error = ex.Message });
        }
    }


    [HttpPost("register")]
    public async Task<ActionResult> Register([FromBody] AuthRegisterRequest request)
    {
        try
        {
            // Validar datos básicos
            if (string.IsNullOrWhiteSpace(request.Name) || string.IsNullOrWhiteSpace(request.Email))
            {
                return BadRequest(new { success = false, message = "Nombre y email son obligatorios" });
            }

            if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 6)
            {
                return BadRequest(new { success = false, message = "La contraseña debe tener al menos 6 caracteres" });
            }

            // Verificar si el email ya existe - CONSULTA CORREGIDA
            var emailExists = await _context.Estudiantes
                .AnyAsync(e => e.Email.ToLower() == request.Email.ToLower());

            if (emailExists)
            {
                return BadRequest(new { success = false, message = "El email ya está registrado" });
            }

            // CREAR FECHAS SIN UTC PARA EVITAR PROBLEMAS CON POSTGRESQL
            var currentTime = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

            // Crear nuevo estudiante
            var estudiante = new Estudiante
            {
                Nombre = request.Name.Trim(),
                SegundoNombre = request.SegundoNombre?.Trim(),
                ApellidoPaterno = request.ApellidoPaterno?.Trim(),
                ApellidoMaterno = request.ApellidoMaterno?.Trim(),
                Email = request.Email.ToLower().Trim(),
                PasswordHash = HashPassword(request.Password),
                FechaRegistro = currentTime,
                UltimoAcceso = currentTime,
                Activo = true,
                Verificado = false
                // NO asignar NivelActual ni NivelDiagnosticado - la BD usa sus defaults
            };

            // Guardar en base de datos
            _context.Estudiantes.Add(estudiante);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Usuario registrado exitosamente: {Email}", request.Email);

            return Ok(new
            {
                success = true,
                message = "Usuario registrado exitosamente",
                user = new
                {
                    id = estudiante.Id,
                    name = estudiante.Nombre,
                    email = estudiante.Email,
                    fechaRegistro = estudiante.FechaRegistro
                }
            });
        }
        catch (DbUpdateException dbEx)
        {
            _logger.LogError(dbEx, "Error de base de datos al registrar usuario: {Email}", request.Email);
            return StatusCode(500, new { success = false, message = "Error en la base de datos" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registrando usuario: {Email}", request.Email);
            return StatusCode(500, new { success = false, message = "Error interno del servidor" });
        }
    }

    // POST: api/auth/login
    [HttpPost("login")]
    public async Task<ActionResult> Login([FromBody] AuthLoginRequest request)
    {
        try
        {
            // Validar datos
            if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            {
                return BadRequest(new { success = false, message = "Email y contraseña son obligatorios" });
            }

            // Buscar usuario - CORREGIDO para manejar bool?
            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Email.ToLower() == request.Email.ToLower() && e.Activo == true);

            if (estudiante == null)
            {
                return BadRequest(new { success = false, message = "Credenciales incorrectas" });
            }

            // Verificar contraseña
            if (!VerifyPassword(request.Password, estudiante.PasswordHash))
            {
                return BadRequest(new { success = false, message = "Credenciales incorrectas" });
            }

            // ACTUALIZAR ÚLTIMO ACCESO SIN UTC
            estudiante.UltimoAcceso = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Login exitoso: {Email}", request.Email);

            return Ok(new
            {
                success = true,
                message = "Login exitoso",
                user = new
                {
                    id = estudiante.Id,
                    name = estudiante.Nombre,
                    email = estudiante.Email,
                    fechaRegistro = estudiante.FechaRegistro
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en login: {Email}", request.Email);
            return StatusCode(500, new { success = false, message = "Error interno del servidor" });
        }
    }

    // POST: api/auth/complete-diagnostic
    [HttpPost("complete-diagnostic")]
    public async Task<ActionResult> CompleteDiagnosticTest([FromBody] CompleteDiagnosticRequest request)
    {
        try
        {
            // Buscar el estudiante
            var estudiante = await _context.Estudiantes
                .FirstOrDefaultAsync(e => e.Id == request.EstudianteId && e.Activo == true);

            if (estudiante == null)
            {
                return NotFound(new { success = false, message = "Estudiante no encontrado" });
            }

            // Verificar que no haya completado ya el test
            if (estudiante.TestDiagnosticoCompletado == true)
            {
                return BadRequest(new { success = false, message = "Test diagnóstico ya completado" });
            }

            // Actualizar los campos del test diagnóstico
            var currentTime = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);

            estudiante.TestDiagnosticoCompletado = true;
            estudiante.FechaTestDiagnostico = currentTime;
            estudiante.UltimoAcceso = currentTime;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Test diagnóstico completado para estudiante: {EstudianteId}", request.EstudianteId);

            return Ok(new
            {
                success = true,
                message = "Test diagnóstico completado exitosamente",
                data = new
                {
                    estudianteId = estudiante.Id,
                    fechaCompletado = estudiante.FechaTestDiagnostico,
                    testCompletado = estudiante.TestDiagnosticoCompletado
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completando test diagnóstico para estudiante: {EstudianteId}", request.EstudianteId);
            return StatusCode(500, new { success = false, message = "Error interno del servidor" });
        }
    }

    // GET: api/auth/diagnostic-status/{estudianteId}
    [HttpGet("diagnostic-status/{estudianteId}")]
    public async Task<ActionResult> GetDiagnosticStatus(int estudianteId)
    {
        try
        {
            var estudiante = await _context.Estudiantes
                .Where(e => e.Id == estudianteId && e.Activo == true)
                .Select(e => new {
                    e.Id,
                    e.TestDiagnosticoCompletado,
                    e.FechaTestDiagnostico
                })
                .FirstOrDefaultAsync();

            if (estudiante == null)
            {
                return NotFound(new { success = false, message = "Estudiante no encontrado" });
            }

            return Ok(new
            {
                success = true,
                data = new
                {
                    estudianteId = estudiante.Id,
                    testCompletado = estudiante.TestDiagnosticoCompletado ?? false,
                    fechaCompletado = estudiante.FechaTestDiagnostico,
                    requiereTest = estudiante.TestDiagnosticoCompletado != true
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error obteniendo estado diagnóstico: {EstudianteId}", estudianteId);
            return StatusCode(500, new { success = false, message = "Error interno del servidor" });
        }
    }

    // Métodos auxiliares para hash de contraseñas
    private string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private bool VerifyPassword(string password, string hash)
    {
        var hashedPassword = HashPassword(password);
        return hashedPassword == hash;
    }
}



// DTOs para AuthController
public class AuthRegisterRequest
{
    public string Name { get; set; } = string.Empty;
    public string? SegundoNombre { get; set; }
    public string? ApellidoPaterno { get; set; }
    public string? ApellidoMaterno { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AuthLoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

// DTO para completar test diagnóstico
public class CompleteDiagnosticRequest
{
    public int EstudianteId { get; set; }
    // Podrías agregar más campos como resultados del test, nivel asignado, etc.
    public string? ResultadoNivel { get; set; }
    public int? PuntajeObtenido { get; set; }
}