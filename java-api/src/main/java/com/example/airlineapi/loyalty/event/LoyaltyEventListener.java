package com.example.airlineapi.loyalty.event;

import com.example.airlineapi.identity.event.PassengerRegisteredEvent;
import com.example.airlineapi.loyalty.LoyaltyService;
import org.springframework.context.event.EventListener;
import org.springframework.stereotype.Component;

@Component
public class LoyaltyEventListener {

    private final LoyaltyService loyaltyService;

    public LoyaltyEventListener(LoyaltyService loyaltyService) {
        this.loyaltyService = loyaltyService;
    }

    @EventListener
    public void onPassengerRegistered(PassengerRegisteredEvent event) {
        loyaltyService.createAccount(event.getUsername());
    }
}
