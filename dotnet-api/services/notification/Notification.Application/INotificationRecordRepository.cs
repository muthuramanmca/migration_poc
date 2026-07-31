using Notification.Domain;

namespace Notification.Application;

public interface INotificationRecordRepository
{
    Task AddAsync(NotificationRecord record, CancellationToken cancellationToken = default);
}
