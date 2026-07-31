package com.example.airlineapi.booking.event;

import com.example.airlineapi.loyalty.LoyaltyService;
import com.example.airlineapi.notification.NotificationService;
import org.springframework.context.event.EventListener;
import org.springframework.stereotype.Component;

@Component
public class BookingEventListener {

    private final NotificationService notificationService;
    private final LoyaltyService loyaltyService;

    public BookingEventListener(NotificationService notificationService, LoyaltyService loyaltyService) {
        this.notificationService = notificationService;
        this.loyaltyService = loyaltyService;
    }

    @EventListener
    public void onBookingCreated(BookingCreatedEvent event) {
        notificationService.send(event.getBookingId(), "BOOKING_CREATED");
    }

    @EventListener
    public void onBookingPaid(BookingPaidEvent event) {
        notificationService.send(event.getBookingId(), "PAYMENT_CONFIRMED");
        loyaltyService.awardMiles(event.getUsername(), event.getTotalFare());
    }
}
