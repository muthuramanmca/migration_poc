package com.example.airlineapi.booking.event;

import org.springframework.context.ApplicationEvent;

import java.math.BigDecimal;

/**
 * Fired synchronously inside BookingService.pay()'s open transaction, before commit -- a second
 * instance of the same event-inside-transaction antipattern as BookingCreatedEvent, reinforcing
 * the same migration lesson at a second point in the lifecycle. Consumed by BookingEventListener
 * to both log a payment-confirmation notification and award loyalty miles.
 */
public class BookingPaidEvent extends ApplicationEvent {
    private final Long bookingId;
    private final String username;
    private final BigDecimal totalFare;

    public BookingPaidEvent(Object source, Long bookingId, String username, BigDecimal totalFare) {
        super(source);
        this.bookingId = bookingId;
        this.username = username;
        this.totalFare = totalFare;
    }

    public Long getBookingId() { return bookingId; }
    public String getUsername() { return username; }
    public BigDecimal getTotalFare() { return totalFare; }
}
