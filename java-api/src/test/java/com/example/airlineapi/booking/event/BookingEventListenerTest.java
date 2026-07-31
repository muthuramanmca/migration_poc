package com.example.airlineapi.booking.event;

import com.example.airlineapi.loyalty.LoyaltyService;
import com.example.airlineapi.notification.NotificationService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.math.BigDecimal;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.never;
import static org.mockito.Mockito.verify;

/** Proves the cross-domain fan-out: BookingService itself has no dependency on notification/loyalty -- only this listener does. */
class BookingEventListenerTest {

    private NotificationService notificationService;
    private LoyaltyService loyaltyService;
    private BookingEventListener listener;

    @BeforeEach
    void setUp() {
        notificationService = mock(NotificationService.class);
        loyaltyService = mock(LoyaltyService.class);
        listener = new BookingEventListener(notificationService, loyaltyService);
    }

    @Test
    void onBookingCreated_notifiesButDoesNotAwardMiles() {
        listener.onBookingCreated(new BookingCreatedEvent(this, 1L, "alice"));

        verify(notificationService).send(1L, "BOOKING_CREATED");
        verify(loyaltyService, never()).awardMiles(anyString(), any());
    }

    @Test
    void onBookingPaid_notifiesAndAwardsMiles() {
        listener.onBookingPaid(new BookingPaidEvent(this, 1L, "alice", BigDecimal.valueOf(200)));

        verify(notificationService).send(1L, "PAYMENT_CONFIRMED");
        verify(loyaltyService).awardMiles("alice", BigDecimal.valueOf(200));
    }
}
