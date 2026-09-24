namespace Pegadas.Modules.Notifications.Contracts;

/// <summary>
/// Queues a notification for delivery. Delivery always happens in a background job, never
/// inside the HTTP request, and each notification carries an idempotency key.
/// </summary>
public interface INotificationSender
{
    Task SendAsync(NotificationRequest request, CancellationToken cancellationToken);
}

/// <param name="SchoolId">Tenant.</param>
/// <param name="RecipientId">Staff member or (future) guardian id.</param>
/// <param name="Template">Template key, e.g. <c>daily-summary-ready</c>.</param>
/// <param name="Data">Template values; ids and links only, never personal data.</param>
/// <param name="IdempotencyKey">Same key, same notification: resending is a no-op.</param>
public sealed record NotificationRequest(
    Guid SchoolId,
    Guid RecipientId,
    string Template,
    IReadOnlyDictionary<string, string> Data,
    string IdempotencyKey);

/// <summary>Push channel (Expo Push or FCM). Payloads must never contain personal data.</summary>
public interface IPushChannel
{
    Task SendAsync(PushMessage message, CancellationToken cancellationToken);
}

public sealed record PushMessage(string DeviceToken, string Title, string Body);

/// <summary>Transactional email channel (EU-region provider).</summary>
public interface IEmailChannel
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

public sealed record EmailMessage(string To, string Subject, string HtmlBody, string? TextBody = null);
