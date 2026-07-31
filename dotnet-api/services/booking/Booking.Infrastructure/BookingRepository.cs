using Booking.Application;
using Microsoft.EntityFrameworkCore;
using BookingEntity = Booking.Domain.Booking;

namespace Booking.Infrastructure;

public class BookingRepository(BookingDbContext dbContext) : IBookingRepository
{
    public Task<BookingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Bookings.AsNoTracking().FirstOrDefaultAsync(b => b.Id == id, cancellationToken);

    public async Task AddAsync(BookingEntity booking, CancellationToken cancellationToken = default)
    {
        dbContext.Bookings.Add(booking);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
