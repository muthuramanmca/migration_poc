package com.example.airlineapi.loyalty;

import com.example.airlineapi.common.ApiException;
import com.example.airlineapi.loyalty.dto.LoyaltyDtos.LoyaltyAccountResponse;
import com.example.airlineapi.loyalty.dto.LoyaltyDtos.RedeemRequest;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.test.util.ReflectionTestUtils;

import java.math.BigDecimal;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class LoyaltyServiceTest {

    private LoyaltyAccountRepository loyaltyAccountRepository;
    private LoyaltyService loyaltyService;

    @BeforeEach
    void setUp() {
        loyaltyAccountRepository = mock(LoyaltyAccountRepository.class);
        loyaltyService = new LoyaltyService(loyaltyAccountRepository);
        ReflectionTestUtils.setField(loyaltyService, "silverThreshold", 100L);
        ReflectionTestUtils.setField(loyaltyService, "goldThreshold", 500L);
    }

    @Test
    void createAccount_startsAtZeroMilesAndStandardTier() {
        LoyaltyAccountResponse response = loyaltyService.createAccount("alice");

        assertThat(response.milesBalance()).isZero();
        assertThat(response.tier()).isEqualTo(LoyaltyTier.STANDARD);
    }

    @Test
    void awardMiles_upgradesTierWhenThresholdCrossed() {
        LoyaltyAccount account = new LoyaltyAccount("bob");
        when(loyaltyAccountRepository.findByUsername("bob")).thenReturn(Optional.of(account));

        LoyaltyAccountResponse response = loyaltyService.awardMiles("bob", BigDecimal.valueOf(150));

        assertThat(response.milesBalance()).isEqualTo(150);
        assertThat(response.tier()).isEqualTo(LoyaltyTier.SILVER);
    }

    @Test
    void awardMiles_usesFlooredFareAsMiles() {
        LoyaltyAccount account = new LoyaltyAccount("carl");
        when(loyaltyAccountRepository.findByUsername("carl")).thenReturn(Optional.of(account));

        LoyaltyAccountResponse response = loyaltyService.awardMiles("carl", BigDecimal.valueOf(49.99));

        assertThat(response.milesBalance()).isEqualTo(49);
    }

    @Test
    void redeem_hasNoValidationAgainstBalance() {
        LoyaltyAccount account = new LoyaltyAccount("dave");
        account.addMiles(10);
        when(loyaltyAccountRepository.findByUsername("dave")).thenReturn(Optional.of(account));

        LoyaltyAccountResponse response = loyaltyService.redeem("dave", new RedeemRequest(50));

        // Deliberately planted gap: redeeming more than the balance silently goes negative.
        assertThat(response.milesBalance()).isEqualTo(-40);
    }

    @Test
    void getByUsername_throwsNotFoundForUnknownAccount() {
        when(loyaltyAccountRepository.findByUsername("ghost")).thenReturn(Optional.empty());

        assertThatThrownBy(() -> loyaltyService.getByUsername("ghost"))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("not found");
    }
}
