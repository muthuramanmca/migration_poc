package com.example.airlineapi.flight;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.Transient;
import jakarta.persistence.UniqueConstraint;

import java.math.BigDecimal;
import java.time.Instant;

@Entity
@Table(name = "flights", uniqueConstraints = @UniqueConstraint(columnNames = "flightNumber"))
public class Flight {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private String flightNumber;

    @Column(nullable = false)
    private String origin;

    @Column(nullable = false)
    private String destination;

    @Column(nullable = false)
    private Instant departureAt;

    @Column(nullable = false)
    private BigDecimal fare;

    @Column(nullable = false)
    private int seatCapacity;

    @Column(nullable = false)
    private boolean active = true;

    // Populated at read-time by FlightService from application.yml
    // (app.flights.low-seat-threshold) -- not persisted.
    @Transient
    private int lowSeatThreshold = 10;

    protected Flight() {
        // JPA
    }

    public Flight(String flightNumber, String origin, String destination, Instant departureAt,
                  BigDecimal fare, int seatCapacity) {
        this.flightNumber = flightNumber;
        this.origin = origin;
        this.destination = destination;
        this.departureAt = departureAt;
        this.fare = fare;
        this.seatCapacity = seatCapacity;
    }

    /**
     * Business logic living in an entity getter rather than the service layer -
     * intentionally included: this is exactly the kind of rule that's easy to
     * miss when a migration only reads Service classes.
     */
    public boolean isLowSeatAvailability() {
        return seatCapacity < lowSeatThreshold;
    }

    public void setLowSeatThreshold(int lowSeatThreshold) {
        this.lowSeatThreshold = lowSeatThreshold;
    }

    public void decreaseSeats(int quantity) {
        this.seatCapacity -= quantity;
    }

    public void increaseSeats(int quantity) {
        this.seatCapacity += quantity;
    }

    /**
     * Soft-delete: a cancelled flight schedule is deactivated, not removed, so past
     * BookingItem.farePaidAtBooking snapshots stay intact for already-flown/booked passengers.
     */
    public void deactivate() {
        this.active = false;
    }

    public Long getId() { return id; }
    public String getFlightNumber() { return flightNumber; }
    public String getOrigin() { return origin; }
    public String getDestination() { return destination; }
    public Instant getDepartureAt() { return departureAt; }
    public BigDecimal getFare() { return fare; }
    public int getSeatCapacity() { return seatCapacity; }
    public boolean isActive() { return active; }
}
