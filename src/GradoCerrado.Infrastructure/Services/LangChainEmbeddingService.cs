// 📁 src/GradoCerrado.Infrastructure/Services/LangChainEmbeddingService.cs
// REEMPLAZAR COMPLETAMENTE

using GradoCerrado.Application.Interfaces;
using GradoCerrado.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using LangChain.Providers.OpenAI;

namespace GradoCerrado.Infrastructure.Services;

public class LangChainEmbeddingService : IEmbeddingService
{
    private readonly OpenAiProvider _provider;
    private readonly OpenAISettings _settings;
    private readonly ILogger<LangChainEmbeddingService> _logger;
    private readonly IRateLimiter _rateLimiter;

    // Límites de OpenAI
    private const int MAX_TEXTS_PER_BATCH = 100; // Límite de OpenAI
    private const int MAX_TOKENS_PER_REQUEST = 8000; // Para text-embedding-ada-002

    public LangChainEmbeddingService(
        IOptions<OpenAISettings> settings,
        ILogger<LangChainEmbeddingService> logger,
        IRateLimiter rateLimiter)
    {
        _settings = settings.Value;
        _logger = logger;
        _rateLimiter = rateLimiter;
        _provider = new OpenAiProvider(_settings.ApiKey);
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text)
    {
        try
        {
            await _rateLimiter.WaitIfNeededAsync();

            var embeddingModel = new OpenAiEmbeddingModel(
                provider: _provider,
                id: "text-embedding-ada-002");

            var response = await embeddingModel.CreateEmbeddingsAsync(text);

            _rateLimiter.RecordRequest();

            return response.Values.First().ToArray();
        }
        catch (Exception ex)
        {
            _rateLimiter.RecordError();
            _logger.LogError(ex, "Error generando embedding");
            throw;
        }
    }// 📁 src/GradoCerrado.Infrastructure/Services/LangChainEmbeddingService.cs
     // REEMPLAZAR EL MÉTODO GenerateEmbeddingsAsync COMPLETO:

    public async Task<List<float[]>> GenerateEmbeddingsAsync(List<string> texts)
    {
        if (!texts.Any())
            return new List<float[]>();

        try
        {
            var allEmbeddings = new List<float[]>();

            // 🔧 PROCESAR EN LOTES MÁS PEQUEÑOS (LangChain/OpenAI tiene límites)
            const int BATCH_SIZE = 20; // Procesar 20 textos por llamada (más seguro)

            var batches = texts
                .Select((text, index) => new { text, index })
                .GroupBy(x => x.index / BATCH_SIZE)
                .Select(g => g.Select(x => x.text).ToList())
                .ToList();

            _logger.LogInformation(
                "🔢 Generando embeddings: {Total} textos en {Batches} lote(s) de máximo {BatchSize}",
                texts.Count, batches.Count, BATCH_SIZE);

            for (int batchIndex = 0; batchIndex < batches.Count; batchIndex++)
            {
                var batch = batches[batchIndex];

                // ✅ ESPERAR SLOT DISPONIBLE
                await _rateLimiter.WaitIfNeededAsync();

                _logger.LogInformation(
                    "📦 Procesando lote {Current}/{Total} ({Count} textos)...",
                    batchIndex + 1, batches.Count, batch.Count);

                // 🔧 PROCESAR CADA TEXTO DEL LOTE SECUENCIALMENTE
                // (LangChain no soporta batch nativo bien)
                var batchEmbeddings = new List<float[]>();

                foreach (var text in batch)
                {
                    var embeddingModel = new OpenAiEmbeddingModel(
                        provider: _provider,
                        id: "text-embedding-ada-002");

                    // Generar embedding para este texto individual
                    var response = await embeddingModel.CreateEmbeddingsAsync(text);
                    var embedding = response.Values.First().ToArray();
                    batchEmbeddings.Add(embedding);

                    // Mini delay entre textos del mismo lote (200ms)
                    if (batch.Count > 1)
                    {
                        await Task.Delay(200);
                    }
                }

                _rateLimiter.RecordRequest();

                allEmbeddings.AddRange(batchEmbeddings);

                _logger.LogInformation(
                    "✅ Lote {Current}/{Total} completado ({Embeddings} embeddings generados)",
                    batchIndex + 1, batches.Count, batchEmbeddings.Count);
            }

            _logger.LogInformation(
                "✅ TOTAL: {Count} embeddings generados exitosamente",
                allEmbeddings.Count);

            return allEmbeddings;
        }
        catch (Exception ex)
        {
            _rateLimiter.RecordError();
            _logger.LogError(ex, "❌ Error generando embeddings en lote");
            throw;
        }
    }

    // 🆕 DIVIDIR EN LOTES INTELIGENTES
    private List<List<string>> SplitIntoBatches(List<string> texts, int maxBatchSize)
    {
        var batches = new List<List<string>>();
        var currentBatch = new List<string>();
        var currentTokenCount = 0;

        foreach (var text in texts)
        {
            // Estimar tokens (1 token ≈ 4 caracteres)
            var estimatedTokens = text.Length / 4;

            // Si agregar este texto excede límites, cerrar lote actual
            if ((currentBatch.Count >= maxBatchSize) ||
                (currentTokenCount + estimatedTokens > MAX_TOKENS_PER_REQUEST))
            {
                if (currentBatch.Any())
                {
                    batches.Add(currentBatch);
                    currentBatch = new List<string>();
                    currentTokenCount = 0;
                }
            }

            currentBatch.Add(text);
            currentTokenCount += estimatedTokens;
        }

        if (currentBatch.Any())
        {
            batches.Add(currentBatch);
        }

        return batches;
    }
}