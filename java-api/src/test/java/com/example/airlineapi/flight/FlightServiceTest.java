package com.example.airlineapi.flight;

import com.example.airlineapi.common.ApiException;
import com.example.airlineapi.flight.dto.FlightDtos.FlightRequest;
import com.example.airlineapi.flight.dto.FlightDtos.FlightResponse;
import com.example.airlineapi.flight.dto.FlightDtos.SeatAdjustmentRequest;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.test.util.ReflectionTestUtils;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class FlightServiceTest {

    private FlightRepository flightRepository;
    private FlightService flightService;

    @BeforeEach
    void setUp() {
        flightRepository = mock(FlightRepository.class);
        flightService = new FlightService(flightRepository);
        ReflectionTestUtils.setField(flightService, "lowSeatThreshold", 10);
    }

    @Test
    void create_rejectsDuplicateFlightNumber() {
        when(flightRepository.existsByFlightNumber("UA100")).thenReturn(true);

        FlightRequest request = new FlightRequest("UA100", "ORD", "SFO", Instant.now(), BigDecimal.TEN, 5);

        assertThatThrownBy(() -> flightService.create(request))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("already exists");
    }

    @Test
    void create_flagsLowSeatAvailabilityWhenBelowThreshold() {
        FlightRequest request = new FlightRequest("UA200", "ORD", "SFO", Instant.now(), BigDecimal.TEN, 3);

        FlightResponse response = flightService.create(request);

        assertThat(response.lowSeatAvailability()).isTrue();
        assertThat(response.seatCapacity()).isEqualTo(3);
    }

    @Test
    void create_doesNotFlagLowSeatAvailabilityAboveThreshold() {
        FlightRequest request = new FlightRequest("UA300", "ORD", "SFO", Instant.now(), BigDecimal.TEN, 50);

        FlightResponse response = flightService.create(request);

        assertThat(response.lowSeatAvailability()).isFalse();
    }

    @Test
    void adjustSeats_rejectsNegativeResultingCapacity() {
        Flight flight = new Flight("UA400", "ORD", "SFO", Instant.now(), BigDecimal.TEN, 5);
        when(flightRepository.findByIdAndActiveTrue(1L)).thenReturn(Optional.of(flight));

        assertThatThrownBy(() -> flightService.adjustSeats(1L, new SeatAdjustmentRequest(-10)))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("negative");
    }

    @Test
    void delete_deactivatesFlightInsteadOfRemovingIt() {
        Flight flight = new Flight("UA500", "ORD", "SFO", Instant.now(), BigDecimal.TEN, 5);
        when(flightRepository.findByIdAndActiveTrue(1L)).thenReturn(Optional.of(flight));

        flightService.delete(1L);

        assertThat(flight.isActive()).isFalse();
    }

    @Test
    void delete_throwsNotFoundWhenAlreadyDeactivated() {
        when(flightRepository.findByIdAndActiveTrue(1L)).thenReturn(Optional.empty());

        assertThatThrownBy(() -> flightService.delete(1L))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("not found");
    }

    @Test
    void findOrThrow_excludesDeactivatedFlights() {
        when(flightRepository.findByIdAndActiveTrue(1L)).thenReturn(Optional.empty());

        assertThatThrownBy(() -> flightService.getById(1L))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("not found");
    }
}
