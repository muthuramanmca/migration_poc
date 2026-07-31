package com.example.airlineapi.identity.event;

import org.springframework.context.ApplicationEvent;

/**
 * Fired synchronously inside IdentityService.register()'s open transaction, before commit --
 * the same event-inside-transaction pattern used across every domain in this app (see
 * BookingCreatedEvent/BookingPaidEvent). Consumed by loyalty.event.LoyaltyEventListener to
 * auto-create a LoyaltyAccount; identity itself has no dependency on the loyalty package.
 *
 * Carries the username (not the numeric id) to match the natural-key convention already used by
 * Booking (see booking.Booking.username) -- avoids a username-to-id lookup anywhere downstream.
 */
public class PassengerRegisteredEvent extends ApplicationEvent {

    private final String username;

    public PassengerRegisteredEvent(Object source, String username) {
        super(source);
        this.username = username;
    }

    public String getUsername() {
        return username;
    }
}
