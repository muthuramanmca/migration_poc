using BookingEntity = Booking.Domain.Booking;

namespace Booking.Application;

/// <summary>Signatures only -- implementation (Booking.Infrastructure) and real booking-creation logic land with Booking's business-logic pass.</summary>
public interface IBookingRepository
{
    Task<BookingEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(BookingEntity booking, CancellationToken cancellationToken = default);
}
