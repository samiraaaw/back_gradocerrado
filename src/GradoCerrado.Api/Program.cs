using GradoCerrado.Infrastructure;
using GradoCerrado.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using DocumentFormat.OpenXml.Office2016.Drawing.ChartDrawing;
using GradoCerrado.Application.Interfaces;
using GradoCerrado.Infrastructure.Repositories;
using GradoCerrado.Infrastructure.Services;

// ✅ CONFIGURACIÓN GLOBAL PARA NPGSQL DATETIME (CRÍTICO PARA AZURE POSTGRESQL)
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// ✅ CREAR EL BUILDER
var builder = WebApplication.CreateBuilder(args);

// ✅ USAR builder.Configuration
var dataSourceBuilder = new NpgsqlDataSourceBuilder(
    builder.Configuration.GetConnectionString("DefaultConnection")
);

// ✅ REGISTRAR ENUMS
dataSourceBuilder.MapEnum<TipoPregunta>("tipo_pregunta");
dataSourceBuilder.MapEnum<NivelDificultad>("nivel_dificultad");
dataSourceBuilder.MapEnum<EstadoTest>("estado_test");
dataSourceBuilder.MapEnum<PrioridadNotificacion>("prioridad_notificacion");

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// ✅ CONFIGURACIÓN DE BASE DE DATOS AZURE POSTGRESQL
builder.Services.AddDbContext<GradocerradoContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new InvalidOperationException("Connection string 'DefaultConnection' not found in configuration.");
    }

    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        // ✅ CONFIGURACIONES ESPECÍFICAS PARA AZURE POSTGRESQL
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorCodesToAdd: null);

        // ✅ TIMEOUT AUMENTADO PARA AZURE
        npgsqlOptions.CommandTimeout(60);
    });

    // ✅ LOGGING DE SQL EN DESARROLLO
    if (builder.Environment.IsDevelopment())
    {
        options.EnableSensitiveDataLogging();
        options.LogTo(Console.WriteLine, LogLevel.Information);
    }
});



// ✅ SERVICIOS DE INFRASTRUCTURE (OpenAI + Qdrant)  
builder.Services.AddInfrastructure(builder.Configuration);

// ✅ CONFIGURACIÓN DE CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowIonic", policy =>
    {
        policy.WithOrigins("http://localhost:8100", "http://localhost:8101")  // Frontend Ionic
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
// 🆕 REGISTRAR REPOSITORIOS
builder.Services.AddScoped<ITestRepository, TestRepository>();
builder.Services.AddScoped<IPreguntaRepository, PreguntaRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// ✅ IMPORTANTE: CORS antes de Authorization
app.UseCors("AllowIonic");

app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/api/status", () => new {
    status = "OK",
    timestamp = DateTime.Now, // ✅ Usar DateTime.Now sin UTC
    message = "Backend funcionando correctamente con Azure PostgreSQL"
});

// 🔧 ENDPOINT DE TESTING PARA VERIFICAR AZURE BD
app.MapGet("/api/test-db", async (GradocerradoContext context) =>
{
    try
    {
        var canConnect = await context.Database.CanConnectAsync();
        var studentCount = await context.Estudiantes.CountAsync();

        return Results.Ok(new
        {
            canConnect,
            studentCount,
            message = canConnect ? "Conexión exitosa a Azure PostgreSQL" : "Error de conexión",
            timestamp = DateTime.Now, // ✅ Sin UTC
            database = "Azure PostgreSQL"
        });
    }
    catch (Exception ex)
    {
        return Results.Problem($"Error conectando a Azure PostgreSQL: {ex.Message}");
    }
});

// 🔧 ENDPOINT PARA VERIFICAR CONFIGURACIÓN
app.MapGet("/api/config-check", (IConfiguration config) =>
{
    var connectionString = config.GetConnectionString("DefaultConnection");
    var hasConnection = !string.IsNullOrEmpty(connectionString);
    var isAzure = connectionString?.Contains("postgres.database.azure.com") ?? false;

    return Results.Ok(new
    {
        hasConnectionString = hasConnection,
        isAzurePostgreSQL = isAzure,
        host = isAzure ? "Azure PostgreSQL" : "Local/Other",
        timestamp = DateTime.Now
    });
});

app.Run();