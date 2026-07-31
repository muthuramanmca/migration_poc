package com.example.airlineapi.booking.dto;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;

public class BookingDtos {

    public record BookingLineRequest(
            @NotNull Long flightId,
            @Min(1) int seatCount
    ) {}

    public record BookingRequest(
            @NotEmpty @Valid List<BookingLineRequest> items
    ) {}

    public record BookingItemResponse(Long flightId, int seatCount, BigDecimal farePaidAtBooking, BigDecimal lineTotal) {}

    public record BookingResponse(
            Long id,
            String username,
            String status,
            BigDecimal totalFare,
            Instant createdAt,
            List<BookingItemResponse> items
    ) {}
}
