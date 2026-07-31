package com.example.airlineapi.booking.event;

import org.springframework.context.ApplicationEvent;

/**
 * Represents the integration point every migrated module needs a plan for: in the real Java app
 * this might publish to Kafka/RabbitMQ instead of an in-process Spring event. Fired synchronously
 * inside BookingService.create()'s open transaction, before commit -- deliberately, the same
 * antipattern dotnet-api's real transactional outbox fixes. See also BookingPaidEvent.
 */
public class BookingCreatedEvent extends ApplicationEvent {
    private final Long bookingId;
    private final String username;

    public BookingCreatedEvent(Object source, Long bookingId, String username) {
        super(source);
        this.bookingId = bookingId;
        this.username = username;
    }

    public Long getBookingId() { return bookingId; }
    public String getUsername() { return username; }
}
