using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using GradoCerrado.Application.Interfaces;
using GradoCerrado.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace GradoCerrado.Infrastructure.Services;

public class FirebasePushNotificationService : IPushNotificationService
{
    private readonly ILogger<FirebasePushNotificationService> _logger;
    private readonly FirebaseSettings _settings;
    private FirebaseApp? _firebaseApp;

    public FirebasePushNotificationService(
        IOptions<FirebaseSettings> settings,
        ILogger<FirebasePushNotificationService> logger)
    {
        _settings = settings.Value;
        _logger = logger;
        InitializeFirebase();
    }

    private void InitializeFirebase()
    {
        try
        {
            if (FirebaseApp.DefaultInstance == null)
            {
                // Crear credencial desde configuración
                var credential = GoogleCredential.FromJson(JsonSerializer.Serialize(new
                {
                    type = _settings.Type,
                    project_id = _settings.ProjectId,
                    private_key_id = _settings.PrivateKeyId,
                    private_key = _settings.PrivateKey,
                    client_email = _settings.ClientEmail,
                    client_id = _settings.ClientId
                }));

                _firebaseApp = FirebaseApp.Create(new AppOptions
                {
                    Credential = credential,
                    ProjectId = _settings.ProjectId
                });

                _logger.LogInformation("✅ Firebase inicializado correctamente");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error inicializando Firebase");
        }
    }

    public async Task<bool> SendPushNotificationAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(deviceToken))
            {
                _logger.LogWarning("Token de dispositivo vacío");
                return false;
            }

            var message = new Message
            {
                Token = deviceToken,
                Notification = new Notification
                {
                    Title = title,
                    Body = body
                },
                Data = data,
                Android = new AndroidConfig
                {
                    Priority = Priority.High,
                    Notification = new AndroidNotification
                    {
                        Sound = "default",
                        ChannelId = "study_reminders"
                    }
                },
                Apns = new ApnsConfig
                {
                    Aps = new Aps
                    {
                        Sound = "default",
                        Badge = 1
                    }
                }
            };

            var response = await FirebaseMessaging.DefaultInstance.SendAsync(message);

            _logger.LogInformation(
                "✅ Notificación enviada exitosamente. MessageId: {MessageId}",
                response);

            return true;
        }
        catch (FirebaseMessagingException ex)
        {
            _logger.LogError(ex,
                "❌ Error de Firebase enviando notificación. Código: {Code}",
                ex.MessagingErrorCode);

            // Si el token es inválido, podríamos marcarlo para limpieza
            if (ex.MessagingErrorCode == MessagingErrorCode.Unregistered ||
                ex.MessagingErrorCode == MessagingErrorCode.InvalidArgument)
            {
                _logger.LogWarning("⚠️ Token inválido o no registrado: {Token}",
                    deviceToken.Substring(0, Math.Min(20, deviceToken.Length)));
            }

            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error inesperado enviando notificación");
            return false;
        }
    }

    public async Task<(int Success, int Failed)> SendBulkPushNotificationsAsync(
        List<(string Token, string Title, string Body)> notifications)
    {
        int success = 0;
        int failed = 0;

        foreach (var (token, title, body) in notifications)
        {
            var result = await SendPushNotificationAsync(token, title, body);

            if (result)
                success++;
            else
                failed++;

            // Pequeño delay para no saturar Firebase
            await Task.Delay(100);
        }

        _logger.LogInformation(
            "📊 Notificaciones bulk: {Success} exitosas, {Failed} fallidas",
            success, failed);

        return (success, failed);
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            // Intentar enviar a un token de prueba (fallará pero confirma que Firebase está configurado)
            var testToken = "test_token_validation";

            try
            {
                await FirebaseMessaging.DefaultInstance.SendAsync(new Message
                {
                    Token = testToken,
                    Notification = new Notification { Title = "Test", Body = "Test" }
                });
            }
            catch (FirebaseMessagingException ex)
            {
                // Esperamos este error porque el token es inválido
                // Lo importante es que Firebase respondió
                _logger.LogInformation("Firebase respondió correctamente (error esperado de token inválido)");
                return true;
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error en test de conexión Firebase");
            return false;
        }
    }
}