// 📁 src/GradoCerrado.Infrastructure/Services/OpenAIRateLimiter.cs
// REEMPLAZAR COMPLETO CON ESTA VERSIÓN SIMPLIFICADA:

using Microsoft.Extensions.Logging;

namespace GradoCerrado.Infrastructure.Services;

/// <summary>
/// Interfaz genérica para rate limiting
/// </summary>
public interface IRateLimiter
{
    Task WaitIfNeededAsync();
    void RecordRequest();
    void RecordError();
}

/// <summary>
/// Rate limiter para OpenAI API
/// Límites configurables según tier
/// </summary>
public class OpenAIRateLimiter : IRateLimiter
{
    private readonly ILogger<OpenAIRateLimiter> _logger;
    private readonly Queue<DateTime> _requestTimestamps = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // ⚙️ CONFIGURACIÓN - Ajustar según tu tier de OpenAI
    private const int MAX_REQUESTS_PER_MINUTE = 50;  // Tier 1: 50 RPM
    private const int DELAY_BETWEEN_REQUESTS_MS = 1500; // 1.5 segundos
    private const int DELAY_ON_ERROR_MS = 5000; // 5 segundos si hay error

    private DateTime _lastRequestTime = DateTime.MinValue;
    private int _consecutiveErrors = 0;

    public OpenAIRateLimiter(ILogger<OpenAIRateLimiter> logger)
    {
        _logger = logger;
    }

    public async Task WaitIfNeededAsync()
    {
        await _semaphore.WaitAsync();
        try
        {
            var now = DateTime.UtcNow;

            // 1️⃣ LIMPIAR TIMESTAMPS ANTIGUOS (más de 1 minuto)
            while (_requestTimestamps.Count > 0 &&
                   (now - _requestTimestamps.Peek()).TotalMinutes >= 1)
            {
                _requestTimestamps.Dequeue();
            }

            // 2️⃣ VERIFICAR LÍMITE POR MINUTO
            if (_requestTimestamps.Count >= MAX_REQUESTS_PER_MINUTE)
            {
                var oldestRequest = _requestTimestamps.Peek();
                var waitTime = TimeSpan.FromMinutes(1) - (now - oldestRequest);

                if (waitTime.TotalMilliseconds > 0)
                {
                    _logger.LogWarning(
                        "⏸️ Rate limit alcanzado ({Count}/{Max} requests/min). Esperando {Seconds}s",
                        _requestTimestamps.Count,
                        MAX_REQUESTS_PER_MINUTE,
                        (int)waitTime.TotalSeconds);

                    await Task.Delay(waitTime);
                    now = DateTime.UtcNow;

                    // Limpiar después de esperar
                    while (_requestTimestamps.Count > 0 &&
                           (now - _requestTimestamps.Peek()).TotalMinutes >= 1)
                    {
                        _requestTimestamps.Dequeue();
                    }
                }
            }

            // 3️⃣ DELAY MÍNIMO ENTRE REQUESTS
            var timeSinceLastRequest = now - _lastRequestTime;
            var minDelay = _consecutiveErrors > 0
                ? TimeSpan.FromMilliseconds(DELAY_ON_ERROR_MS * _consecutiveErrors)
                : TimeSpan.FromMilliseconds(DELAY_BETWEEN_REQUESTS_MS);

            if (timeSinceLastRequest < minDelay)
            {
                var delayNeeded = minDelay - timeSinceLastRequest;

                if (delayNeeded.TotalMilliseconds > 0)
                {
                    _logger.LogDebug(
                        "⏳ Delay entre requests: {Ms}ms",
                        (int)delayNeeded.TotalMilliseconds);

                    await Task.Delay(delayNeeded);
                }
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public void RecordRequest()
    {
        _requestTimestamps.Enqueue(DateTime.UtcNow);
        _lastRequestTime = DateTime.UtcNow;
        _consecutiveErrors = 0;

        _logger.LogDebug(
            "📊 Request registrado. En ventana: {Count}/{Max}",
            _requestTimestamps.Count,
            MAX_REQUESTS_PER_MINUTE);
    }

    public void RecordError()
    {
        _consecutiveErrors++;
        _logger.LogWarning(
            "❌ Error #{Count}. Delay aumentará en próximos requests.",
            _consecutiveErrors);
    }
}