package com.example.airlineapi.loyalty.dto;

import com.example.airlineapi.loyalty.LoyaltyTier;

public class LoyaltyDtos {

    /** Always redeems from the caller's own account (username comes from the auth token, not this body) -- avoids an ownership gap where one passenger could redeem another's miles. */
    public record RedeemRequest(long miles) {}

    public record LoyaltyAccountResponse(Long id, String username, long milesBalance, LoyaltyTier tier) {}
}
