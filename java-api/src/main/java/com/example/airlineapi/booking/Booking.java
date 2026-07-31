package com.example.airlineapi.booking;

import com.example.airlineapi.common.ApiException;
import jakarta.persistence.CascadeType;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.OneToMany;
import jakarta.persistence.Table;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

@Entity
@Table(name = "bookings")
public class Booking {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private String username;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private BookingStatus status = BookingStatus.PENDING;

    @Column(nullable = false)
    private BigDecimal totalFare = BigDecimal.ZERO;

    @Column(nullable = false)
    private Instant createdAt = Instant.now();

    @OneToMany(mappedBy = "booking", cascade = CascadeType.ALL, orphanRemoval = true, fetch = FetchType.EAGER)
    private List<BookingItem> items = new ArrayList<>();

    protected Booking() {
        // JPA
    }

    public Booking(String username) {
        this.username = username;
    }

    public void addItem(BookingItem item) {
        item.setBooking(this);
        items.add(item);
        recalculateTotal();
    }

    private void recalculateTotal() {
        this.totalFare = items.stream()
                .map(BookingItem::lineTotal)
                .reduce(BigDecimal.ZERO, BigDecimal::add);
    }

    /**
     * The booking status state machine. This is the single place the valid
     * transitions are enforced - deliberately placed on the entity (not the
     * service) as another example of business logic that lives outside the
     * "obvious" Service class.
     *
     * PENDING -> PAID -> TICKETED -> FLOWN
     * PENDING -> CANCELLED
     * PAID    -> CANCELLED (releases seats - see BookingService.cancel)
     */
    public void transitionTo(BookingStatus target) {
        boolean valid = switch (status) {
            case PENDING -> target == BookingStatus.PAID || target == BookingStatus.CANCELLED;
            case PAID -> target == BookingStatus.TICKETED || target == BookingStatus.CANCELLED;
            case TICKETED -> target == BookingStatus.FLOWN;
            case FLOWN, CANCELLED -> false;
        };
        if (!valid) {
            throw ApiException.conflict("INVALID_BOOKING_STATE",
                    "Cannot transition booking from " + status + " to " + target);
        }
        this.status = target;
    }

    public Long getId() { return id; }
    public String getUsername() { return username; }
    public BookingStatus getStatus() { return status; }
    public BigDecimal getTotalFare() { return totalFare; }
    public Instant getCreatedAt() { return createdAt; }
    public List<BookingItem> getItems() { return items; }
}
