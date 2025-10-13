// 📁 src/GradoCerrado.Infrastructure/DependencyInjection.cs
using GradoCerrado.Application.Interfaces;
using GradoCerrado.Infrastructure.Configuration;
using GradoCerrado.Infrastructure.Repositories;
using GradoCerrado.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GradoCerrado.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configurar settings
        services.Configure<OpenAISettings>(
            configuration.GetSection(OpenAISettings.SectionName));
        services.Configure<QdrantSettings>(
            configuration.GetSection(QdrantSettings.SectionName));
        services.Configure<AzureSpeechSettings>(
            configuration.GetSection(AzureSpeechSettings.SectionName));

        // ✅ TODOS LOS SERVICIOS DE IA USAN LANGCHAIN
        services.AddScoped<IAIService, LangChainQuestionService>();
        services.AddScoped<IEmbeddingService, LangChainEmbeddingService>();
        // ═══════════════════════════════════════════════════════
        // 🆕 RATE LIMITER (SINGLETON - COMPARTIDO POR TODA LA APP)
        // ═══════════════════════════════════════════════════════


        // 🆕 RATE LIMITER (SINGLETON para compartir estado entre todos los requests)
        services.AddSingleton<IRateLimiter, OpenAIRateLimiter>();

        // Servicios de vectores y procesamiento
        services.AddScoped<IVectorService, QdrantService>();
        services.AddScoped<IDocumentProcessingService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<DocumentProcessingService>>();
            var openAiSettings = provider.GetRequiredService<IOptions<OpenAISettings>>().Value;
            return new DocumentProcessingService(logger, openAiSettings.ApiKey);
        });
        services.AddScoped<IQuestionPersistenceService, QuestionPersistenceService>();
        services.AddScoped<IQuestionGenerationService, QuestionGenerationService>();
        services.AddScoped<QuestionPersistenceService>();
        services.AddScoped<IDocumentExtractionService, DocumentExtractionService>();
        services.AddScoped<ITextChunkingService, TextChunkingService>();
        services.AddScoped<IMetadataBuilderService, MetadataBuilderService>();

        // Repositorios
        services.AddScoped<ITestRepository, TestRepository>();
        services.AddScoped<IPreguntaRepository, PreguntaRepository>();

        // Speech
        services.AddScoped<ISpeechService, AzureSpeechService>();

        // HttpClient
        services.AddHttpClient();

        // ✅ AGREGAR ESTAS LÍNEAS:

        // Configuración de Firebase
        services.Configure<FirebaseSettings>(
            configuration.GetSection(FirebaseSettings.SectionName));

        services.Configure<NotificationSettings>(
            configuration.GetSection(NotificationSettings.SectionName));

        // Servicio de Push Notifications
        services.AddSingleton<IPushNotificationService, FirebasePushNotificationService>();

        // Servicio Background (Singleton)
        services.AddSingleton<IHostedService, NotificationBackgroundService>();

        return services;
    }
}