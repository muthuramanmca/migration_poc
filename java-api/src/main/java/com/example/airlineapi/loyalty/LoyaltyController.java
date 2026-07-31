package com.example.airlineapi.loyalty;

import com.example.airlineapi.loyalty.dto.LoyaltyDtos.LoyaltyAccountResponse;
import com.example.airlineapi.loyalty.dto.LoyaltyDtos.RedeemRequest;
import org.springframework.security.core.Authentication;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/loyalty")
public class LoyaltyController {

    private final LoyaltyService loyaltyService;

    public LoyaltyController(LoyaltyService loyaltyService) {
        this.loyaltyService = loyaltyService;
    }

    @GetMapping("/me")
    public LoyaltyAccountResponse me(Authentication authentication) {
        return loyaltyService.getByUsername(authentication.getName());
    }

    @PostMapping("/redeem")
    public LoyaltyAccountResponse redeem(Authentication authentication, @RequestBody RedeemRequest request) {
        return loyaltyService.redeem(authentication.getName(), request);
    }

    @GetMapping("/{username}")
    public LoyaltyAccountResponse getForPassenger(@PathVariable String username) {
        return loyaltyService.getByUsername(username);
    }
}
