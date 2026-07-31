using Notification.Application;
using Notification.Domain;

namespace Notification.Infrastructure;

public class NotificationRecordRepository(NotificationDbContext dbContext) : INotificationRecordRepository
{
    public async Task AddAsync(NotificationRecord record, CancellationToken cancellationToken = default)
    {
        dbContext.NotificationRecords.Add(record);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
