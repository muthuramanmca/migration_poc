package com.example.airlineapi.booking;

import com.example.airlineapi.booking.dto.BookingDtos.BookingItemResponse;
import com.example.airlineapi.booking.dto.BookingDtos.BookingLineRequest;
import com.example.airlineapi.booking.dto.BookingDtos.BookingRequest;
import com.example.airlineapi.booking.dto.BookingDtos.BookingResponse;
import com.example.airlineapi.booking.event.BookingCreatedEvent;
import com.example.airlineapi.booking.event.BookingPaidEvent;
import com.example.airlineapi.common.ApiException;
import com.example.airlineapi.flight.Flight;
import com.example.airlineapi.flight.FlightService;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

@Service
public class BookingService {

    private final BookingRepository bookingRepository;
    private final FlightService flightService;
    private final ApplicationEventPublisher eventPublisher;

    public BookingService(BookingRepository bookingRepository, FlightService flightService,
                           ApplicationEventPublisher eventPublisher) {
        this.bookingRepository = bookingRepository;
        this.flightService = flightService;
        this.eventPublisher = eventPublisher;
    }

    @Transactional
    public BookingResponse create(String username, BookingRequest request) {
        Booking booking = new Booking(username);

        for (BookingLineRequest line : request.items()) {
            Flight flight = flightService.findOrThrow(line.flightId());

            if (flight.getSeatCapacity() < line.seatCount()) {
                throw ApiException.conflict("INSUFFICIENT_SEATS",
                        "Not enough seats for flight " + flight.getFlightNumber() +
                                " (requested " + line.seatCount() + ", available " + flight.getSeatCapacity() + ")");
            }

            flight.decreaseSeats(line.seatCount());
            booking.addItem(new BookingItem(flight.getId(), line.seatCount(), flight.getFare()));
        }

        bookingRepository.save(booking);
        eventPublisher.publishEvent(new BookingCreatedEvent(this, booking.getId(), username));
        return toResponse(booking);
    }

    public BookingResponse getById(String requester, boolean isAdmin, Long id) {
        Booking booking = findOrThrow(id);
        assertOwnerOrAdmin(requester, isAdmin, booking);
        return toResponse(booking);
    }

    public List<BookingResponse> listForUser(String requester, boolean isAdmin) {
        // Admins see every booking; regular passengers only ever see their own -
        // enforced here rather than trusting a query parameter.
        List<Booking> bookings = isAdmin ? bookingRepository.findAll() : bookingRepository.findByUsername(requester);
        return bookings.stream().map(this::toResponse).toList();
    }

    @Transactional
    public BookingResponse pay(String requester, boolean isAdmin, Long id) {
        Booking booking = findOrThrow(id);
        assertOwnerOrAdmin(requester, isAdmin, booking);
        booking.transitionTo(BookingStatus.PAID);
        eventPublisher.publishEvent(new BookingPaidEvent(this, booking.getId(), booking.getUsername(), booking.getTotalFare()));
        return toResponse(booking);
    }

    @Transactional
    public BookingResponse ticket(Long id) {
        Booking booking = findOrThrow(id);
        booking.transitionTo(BookingStatus.TICKETED);
        return toResponse(booking);
    }

    @Transactional
    public BookingResponse complete(Long id) {
        Booking booking = findOrThrow(id);
        booking.transitionTo(BookingStatus.FLOWN);
        return toResponse(booking);
    }

    @Transactional
    public BookingResponse cancel(String requester, boolean isAdmin, Long id) {
        Booking booking = findOrThrow(id);
        assertOwnerOrAdmin(requester, isAdmin, booking);

        booking.transitionTo(BookingStatus.CANCELLED);

        // Seats are reserved (decremented) at booking-creation time, before any
        // payment step exists - so cancelling from PENDING releases seats exactly
        // the same way cancelling from PAID does. Easy to get wrong if you assume
        // release only applies to already-paid bookings.
        for (BookingItem item : booking.getItems()) {
            Flight flight = flightService.findOrThrow(item.getFlightId());
            flight.increaseSeats(item.getSeatCount());
        }

        return toResponse(booking);
    }

    private void assertOwnerOrAdmin(String requester, boolean isAdmin, Booking booking) {
        if (!isAdmin && !booking.getUsername().equals(requester)) {
            throw new AccessDeniedException("Not allowed to access this booking");
        }
    }

    private Booking findOrThrow(Long id) {
        return bookingRepository.findById(id)
                .orElseThrow(() -> ApiException.notFound("BOOKING_NOT_FOUND", "Booking not found: " + id));
    }

    private BookingResponse toResponse(Booking booking) {
        List<BookingItemResponse> items = booking.getItems().stream()
                .map(i -> new BookingItemResponse(i.getFlightId(), i.getSeatCount(), i.getFarePaidAtBooking(), i.lineTotal()))
                .toList();
        return new BookingResponse(booking.getId(), booking.getUsername(), booking.getStatus().name(),
                booking.getTotalFare(), booking.getCreatedAt(), items);
    }
}
