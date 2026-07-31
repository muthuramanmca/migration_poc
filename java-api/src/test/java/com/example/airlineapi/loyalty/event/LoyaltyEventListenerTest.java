package com.example.airlineapi.loyalty.event;

import com.example.airlineapi.identity.event.PassengerRegisteredEvent;
import com.example.airlineapi.loyalty.LoyaltyService;
import org.junit.jupiter.api.Test;

import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;

/** Proves identity has no dependency on loyalty -- the coupling is entirely event-mediated, on this side. */
class LoyaltyEventListenerTest {

    @Test
    void onPassengerRegistered_createsLoyaltyAccount() {
        LoyaltyService loyaltyService = mock(LoyaltyService.class);
        LoyaltyEventListener listener = new LoyaltyEventListener(loyaltyService);

        listener.onPassengerRegistered(new PassengerRegisteredEvent(this, "alice"));

        verify(loyaltyService).createAccount("alice");
    }
}
