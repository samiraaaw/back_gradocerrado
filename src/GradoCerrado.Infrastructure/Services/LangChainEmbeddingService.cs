// 📁 src/GradoCerrado.Infrastructure/Services/LangChainEmbeddingService.cs
using GradoCerrado.Application.Interfaces;
using GradoCerrado.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using LangChain.Providers.OpenAI;

namespace GradoCerrado.Infrastructure.Services;

/// <summary>
/// Servicio de embeddings usando LangChain + OpenAI
/// </summary>
public class LangChainEmbeddingService : IEmbeddingService
{
    private readonly OpenAiProvider _provider;
    private readonly OpenAISettings _settings;
    private readonly ILogger<LangChainEmbeddingService> _logger;

    public LangChainEmbeddingService(
        IOptions<OpenAISettings> settings,
        ILogger<LangChainEmbeddingService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        _provider = new OpenAiProvider(_settings.ApiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            // ✅ Usar modelo de embeddings de LangChain
            var embeddingModel = new OpenAiEmbeddingModel(
                provider: _provider,
                id: "text-embedding-ada-002");

            // ✅ Generar embedding
            var response = await embeddingModel.CreateEmbeddingsAsync(text);

            // ✅ Retornar como float[]
            return response.Values.First().ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando embedding para texto de {Length} caracteres", text.Length);
            throw;
        }
    }

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        try
        {
            var embeddings = new List<float[]>();

            // Procesar en lotes para evitar límites de rate
            const int batchSize = 5;
            for (int i = 0; i < texts.Count; i += batchSize)
            {
                var batch = texts.Skip(i).Take(batchSize).ToList();

                // Procesar batch en paralelo
                var batchTasks = batch.Select(text => GenerateEmbeddingAsync(text));
                var batchResults = await Task.WhenAll(batchTasks);

                embeddings.AddRange(batchResults);

                // Pequeña pausa entre lotes
                if (i + batchSize < texts.Count)
                {
                    await Task.Delay(100);
                }

                _logger.LogInformation(
                    "Procesados {Current}/{Total} embeddings",
                    Math.Min(i + batchSize, texts.Count),
                    texts.Count);
            }

            return embeddings;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando embeddings en lote");
            throw;
        }
    }
}