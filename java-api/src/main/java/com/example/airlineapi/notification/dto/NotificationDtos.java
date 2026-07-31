package com.example.airlineapi.notification.dto;

import java.time.Instant;

public class NotificationDtos {

    public record NotificationLogResponse(Long id, Long bookingId, String type, Instant sentAt) {}
}
