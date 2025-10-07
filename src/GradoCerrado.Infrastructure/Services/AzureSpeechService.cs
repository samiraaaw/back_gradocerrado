using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using GradoCerrado.Application.Interfaces;
using GradoCerrado.Infrastructure.Configuration;
using NAudio.Wave;


namespace GradoCerrado.Infrastructure.Services;

public class AzureSpeechService : ISpeechService
{
    private readonly SpeechConfig _speechConfig;
    private readonly AzureSpeechSettings _settings;
    private readonly ILogger<AzureSpeechService> _logger;

    public AzureSpeechService(
        IOptions<AzureSpeechSettings> settings,
        ILogger<AzureSpeechService> logger)
    {
        _settings = settings.Value;
        _logger = logger;

        // 🔧 Configurar Azure Speech
        _speechConfig = SpeechConfig.FromSubscription(_settings.ApiKey, _settings.Region);
        _speechConfig.SpeechSynthesisVoiceName = _settings.DefaultVoice;

        // 🎯 Configurar formato de audio
        _speechConfig.SetSpeechSynthesisOutputFormat(SpeechSynthesisOutputFormat.Audio16Khz32KBitRateMonoMp3);
    }

    // TEXT-TO-SPEECH
    public async Task<byte[]> TextToSpeechAsync(string text, string? voice = null)
    {
        try
        {
            _logger.LogInformation("Generando audio para texto: {Text}", text[..Math.Min(50, text.Length)]);

            using var synthesizer = new SpeechSynthesizer(_speechConfig);

            //Usar voz específica si se proporciona
            if (!string.IsNullOrEmpty(voice))
            {
                _speechConfig.SpeechSynthesisVoiceName = voice;
            }

            //Sintetizar audio
            using var result = await synthesizer.SpeakTextAsync(text);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                _logger.LogInformation("Audio generado exitosamente. Tamaño: {Size} bytes", result.AudioData.Length);
                return result.AudioData;
            }
            else
            {
                var errorMessage = $"Error en síntesis: {result.Reason}";
                _logger.LogError(errorMessage);
                throw new Exception(errorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando audio para texto");
            throw;
        }
    }

    //SSML para pronunciación avanzada
    public async Task<byte[]> SsmlToSpeechAsync(string ssml)
    {
        try
        {
            _logger.LogInformation("Generando audio desde SSML");

            using var synthesizer = new SpeechSynthesizer(_speechConfig);
            using var result = await synthesizer.SpeakSsmlAsync(ssml);

            if (result.Reason == ResultReason.SynthesizingAudioCompleted)
            {
                _logger.LogInformation("Audio SSML generado exitosamente");
                return result.AudioData;
            }
            else
            {
                throw new Exception($"Error en síntesis SSML: {result.Reason}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generando audio desde SSML");
            throw;
        }
    }

    public async Task<string> SpeechToTextAsync(byte[] audioData)
    {
        try
        {
            _logger.LogInformation("Transcribiendo audio. Tamaño: {Size} bytes", audioData.Length);

            // Configurar idioma
            _speechConfig.SpeechRecognitionLanguage = "es-ES";

            // 🆕 DETECTAR FORMATO Y CONVERTIR SI ES NECESARIO
            var wavData = await ConvertToWavIfNeededAsync(audioData);

            // Validar que es un WAV válido
            if (wavData.Length < 44 ||
                System.Text.Encoding.ASCII.GetString(wavData, 0, 4) != "RIFF")
            {
                throw new Exception("El archivo no pudo ser convertido a WAV válido");
            }

            var tempFile = Path.GetTempFileName() + ".wav";

            try
            {
                await File.WriteAllBytesAsync(tempFile, wavData);

                using var audioConfig = AudioConfig.FromWavFileInput(tempFile);
                using var recognizer = new SpeechRecognizer(_speechConfig, audioConfig);

                recognizer.Properties.SetProperty(PropertyId.SpeechServiceConnection_InitialSilenceTimeoutMs, "10000");
                recognizer.Properties.SetProperty(PropertyId.SpeechServiceConnection_EndSilenceTimeoutMs, "10000");

                var result = await recognizer.RecognizeOnceAsync();

                _logger.LogInformation("Resultado del reconocimiento: {Reason}", result.Reason);

                switch (result.Reason)
                {
                    case ResultReason.RecognizedSpeech:
                        _logger.LogInformation("Transcripción exitosa: {Text}", result.Text);
                        return result.Text;

                    case ResultReason.NoMatch:
                        _logger.LogWarning("No se detectó habla");
                        return "";

                    case ResultReason.Canceled:
                        var cancellation = CancellationDetails.FromResult(result);
                        _logger.LogError("Cancelado: {Reason} - {ErrorDetails}",
                            cancellation.Reason, cancellation.ErrorDetails);
                        return "";

                    default:
                        _logger.LogWarning("Resultado inesperado: {Reason}", result.Reason);
                        return "";
                }
            }
            finally
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transcribiendo audio");
            throw;
        }
    }

    // 🆕 MÉTODO PARA CONVERTIR A WAV
    private async Task<byte[]> ConvertToWavIfNeededAsync(byte[] audioData)
    {
        try
        {
            // Detectar si ya es WAV
            if (audioData.Length >= 4 &&
                System.Text.Encoding.ASCII.GetString(audioData, 0, 4) == "RIFF")
            {
                _logger.LogInformation("Audio ya está en formato WAV");
                return audioData;
            }

            // Detectar si es WebM u otro formato
            _logger.LogInformation("Convirtiendo audio a WAV...");

            var inputFile = Path.GetTempFileName();
            var outputFile = Path.GetTempFileName() + ".wav";

            try
            {
                // Guardar archivo original
                await File.WriteAllBytesAsync(inputFile, audioData);

                // Convertir usando NAudio
                using (var reader = new MediaFoundationReader(inputFile))
                {
                    // Configurar formato WAV compatible con Azure Speech
                    var waveFormat = new WaveFormat(16000, 16, 1); // 16kHz, 16-bit, mono

                    using (var resampler = new MediaFoundationResampler(reader, waveFormat))
                    {
                        WaveFileWriter.CreateWaveFile(outputFile, resampler);
                    }
                }

                // Leer archivo convertido
                var convertedData = await File.ReadAllBytesAsync(outputFile);
                _logger.LogInformation("Audio convertido exitosamente. Nuevo tamaño: {Size} bytes",
                    convertedData.Length);

                return convertedData;
            }
            finally
            {
                if (File.Exists(inputFile)) File.Delete(inputFile);
                if (File.Exists(outputFile)) File.Delete(outputFile);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error convirtiendo audio a WAV");
            throw new Exception("No se pudo convertir el audio a formato WAV compatible", ex);
        }
    }


    //TEST DE CONEXIÓN
    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var testAudio = await TextToSpeechAsync("Test de conexión exitoso");
            return testAudio.Length > 0;
        }
        catch
        {
            return false;
        }
    }
}