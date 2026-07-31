package com.example.airlineapi.loyalty;

import com.example.airlineapi.common.ApiException;
import com.example.airlineapi.loyalty.dto.LoyaltyDtos.LoyaltyAccountResponse;
import com.example.airlineapi.loyalty.dto.LoyaltyDtos.RedeemRequest;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.math.BigDecimal;

@Service
public class LoyaltyService {

    private final LoyaltyAccountRepository loyaltyAccountRepository;

    // Config-driven business rule, same pattern as flight's low-seat threshold.
    @Value("${app.loyalty.silver-threshold}")
    private long silverThreshold;

    @Value("${app.loyalty.gold-threshold}")
    private long goldThreshold;

    public LoyaltyService(LoyaltyAccountRepository loyaltyAccountRepository) {
        this.loyaltyAccountRepository = loyaltyAccountRepository;
    }

    /** Called by LoyaltyEventListener when a PassengerRegisteredEvent arrives -- not exposed as an HTTP endpoint. */
    @Transactional
    public LoyaltyAccountResponse createAccount(String username) {
        LoyaltyAccount account = new LoyaltyAccount(username);
        loyaltyAccountRepository.save(account);
        return toResponse(account);
    }

    /**
     * Called by booking.event.BookingEventListener when a booking transitions to PAID -- uses the
     * fare snapshotted on the BookingItem at booking time, not the flight's current fare, so a
     * later fare change never retroactively changes miles already earned.
     */
    @Transactional
    public LoyaltyAccountResponse awardMiles(String username, BigDecimal farePaid) {
        LoyaltyAccount account = findOrThrow(username);
        account.addMiles(farePaid.longValue());
        updateTier(account);
        return toResponse(account);
    }

    public LoyaltyAccountResponse getByUsername(String username) {
        return toResponse(findOrThrow(username));
    }

    @Transactional
    public LoyaltyAccountResponse redeem(String username, RedeemRequest request) {
        LoyaltyAccount account = findOrThrow(username);
        account.redeem(request.miles());
        return toResponse(account);
    }

    private void updateTier(LoyaltyAccount account) {
        if (account.getMilesBalance() >= goldThreshold) {
            account.setTier(LoyaltyTier.GOLD);
        } else if (account.getMilesBalance() >= silverThreshold) {
            account.setTier(LoyaltyTier.SILVER);
        } else {
            account.setTier(LoyaltyTier.STANDARD);
        }
    }

    private LoyaltyAccount findOrThrow(String username) {
        return loyaltyAccountRepository.findByUsername(username)
                .orElseThrow(() -> ApiException.notFound("LOYALTY_ACCOUNT_NOT_FOUND",
                        "Loyalty account not found for passenger: " + username));
    }

    private LoyaltyAccountResponse toResponse(LoyaltyAccount account) {
        return new LoyaltyAccountResponse(account.getId(), account.getUsername(),
                account.getMilesBalance(), account.getTier());
    }
}
