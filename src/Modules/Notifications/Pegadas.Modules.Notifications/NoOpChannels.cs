using Microsoft.Extensions.Logging;
using Pegadas.Modules.Notifications.Contracts;

namespace Pegadas.Modules.Notifications;

internal sealed partial class NoOpNotificationSender(ILogger<NoOpNotificationSender> logger) : INotificationSender
{
    public Task SendAsync(NotificationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        LogSkipped(logger, request.Template, request.IdempotencyKey);
        return Task.CompletedTask;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Notification {Template} ({IdempotencyKey}) skipped: notifications are disabled")]
    private static partial void LogSkipped(ILogger logger, string template, string idempotencyKey);
}

internal sealed class NoOpPushChannel : IPushChannel
{
    public Task SendAsync(PushMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class NoOpEmailChannel : IEmailChannel
{
    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) => Task.CompletedTask;
}
