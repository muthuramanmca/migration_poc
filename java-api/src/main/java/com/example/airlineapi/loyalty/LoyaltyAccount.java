package com.example.airlineapi.loyalty;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;

@Entity
@Table(name = "loyalty_accounts", uniqueConstraints = @UniqueConstraint(columnNames = "username"))
public class LoyaltyAccount {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private String username;

    @Column(nullable = false)
    private long milesBalance;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private LoyaltyTier tier;

    protected LoyaltyAccount() {
        // JPA
    }

    public LoyaltyAccount(String username) {
        this.username = username;
        this.milesBalance = 0;
        this.tier = LoyaltyTier.STANDARD;
    }

    public void addMiles(long miles) {
        this.milesBalance += miles;
    }

    /**
     * Deliberately no validation against the current balance -- mirrors
     * SeatAdjustmentRequest.delta's missing-validation gap in the flight domain.
     * Redeeming more than the balance silently goes negative.
     */
    public void redeem(long miles) {
        this.milesBalance -= miles;
    }

    public void setTier(LoyaltyTier tier) {
        this.tier = tier;
    }

    public Long getId() { return id; }
    public String getUsername() { return username; }
    public long getMilesBalance() { return milesBalance; }
    public LoyaltyTier getTier() { return tier; }
}
