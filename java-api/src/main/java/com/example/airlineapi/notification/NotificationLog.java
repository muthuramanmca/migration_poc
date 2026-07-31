package com.example.airlineapi.notification;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;

import java.time.Instant;

@Entity
@Table(name = "notification_logs")
public class NotificationLog {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private Long bookingId;

    @Column(nullable = false)
    private String type;

    @Column(nullable = false)
    private Instant sentAt = Instant.now();

    protected NotificationLog() {
        // JPA
    }

    public NotificationLog(Long bookingId, String type) {
        this.bookingId = bookingId;
        this.type = type;
    }

    public Long getId() { return id; }
    public Long getBookingId() { return bookingId; }
    public String getType() { return type; }
    public Instant getSentAt() { return sentAt; }
}
