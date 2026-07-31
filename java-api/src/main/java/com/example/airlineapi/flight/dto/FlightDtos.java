package com.example.airlineapi.flight.dto;

import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.NotNull;

import java.math.BigDecimal;
import java.time.Instant;

public class FlightDtos {

    public record FlightRequest(
            @NotBlank String flightNumber,
            @NotBlank String origin,
            @NotBlank String destination,
            @NotNull Instant departureAt,
            @DecimalMin(value = "0.01", message = "Fare must be greater than zero") BigDecimal fare,
            @Min(value = 0, message = "Initial seat capacity cannot be negative") int seatCapacity
    ) {}

    public record SeatAdjustmentRequest(int delta) {}

    public record FlightResponse(
            Long id,
            String flightNumber,
            String origin,
            String destination,
            Instant departureAt,
            BigDecimal fare,
            int seatCapacity,
            boolean lowSeatAvailability
    ) {}
}
