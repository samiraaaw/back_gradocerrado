namespace GradoCerrado.Application.Interfaces;

public interface IPushNotificationService
{
    /// <summary>
    /// Envía una notificación push a un dispositivo específico
    /// </summary>
    Task<bool> SendPushNotificationAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null);

    /// <summary>
    /// Envía notificaciones push a múltiples dispositivos
    /// </summary>
    Task<(int Success, int Failed)> SendBulkPushNotificationsAsync(
        List<(string Token, string Title, string Body)> notifications);

    /// <summary>
    /// Verifica si Firebase está configurado correctamente
    /// </summary>
    Task<bool> TestConnectionAsync();
}