package com.example.airlineapi.booking;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;

import java.math.BigDecimal;

@Entity
@Table(name = "booking_items")
public class BookingItem {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "booking_id")
    private Booking booking;

    @Column(nullable = false)
    private Long flightId;

    @Column(nullable = false)
    private int seatCount;

    // Fare is captured at booking time so later fare changes on the Flight
    // don't retroactively change historical booking totals (or miles already
    // earned from them -- see LoyaltyService.awardMiles).
    @Column(nullable = false)
    private BigDecimal farePaidAtBooking;

    protected BookingItem() {
        // JPA
    }

    public BookingItem(Long flightId, int seatCount, BigDecimal farePaidAtBooking) {
        this.flightId = flightId;
        this.seatCount = seatCount;
        this.farePaidAtBooking = farePaidAtBooking;
    }

    public BigDecimal lineTotal() {
        return farePaidAtBooking.multiply(BigDecimal.valueOf(seatCount));
    }

    void setBooking(Booking booking) { this.booking = booking; }

    public Long getId() { return id; }
    public Long getFlightId() { return flightId; }
    public int getSeatCount() { return seatCount; }
    public BigDecimal getFarePaidAtBooking() { return farePaidAtBooking; }
}
