namespace Notification.Domain;

/// <summary>Placeholder entity: a log of notifications sent. Real delivery (email/SMS provider integration) lands with Notification's business-logic pass.</summary>
public class NotificationRecord
{
    public Guid Id { get; set; }
    public Guid BookingId { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; set; }
}
