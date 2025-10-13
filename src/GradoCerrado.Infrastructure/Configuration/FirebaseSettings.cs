namespace GradoCerrado.Infrastructure.Configuration;

public class FirebaseSettings
{
    public const string SectionName = "Firebase";

    public string ProjectId { get; set; } = string.Empty;
    public string PrivateKeyId { get; set; } = string.Empty;
    public string PrivateKey { get; set; } = string.Empty;
    public string ClientEmail { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string Type { get; set; } = "service_account";
}

public class NotificationSettings
{
    public const string SectionName = "NotificationSettings";

    public int GenerateAtHour { get; set; } = 6; // 6 AM
    public bool EnableAutoSend { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelayMinutes { get; set; } = 5;
}