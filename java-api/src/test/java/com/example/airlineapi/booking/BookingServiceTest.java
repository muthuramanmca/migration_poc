package com.example.airlineapi.booking;

import com.example.airlineapi.booking.dto.BookingDtos.BookingLineRequest;
import com.example.airlineapi.booking.dto.BookingDtos.BookingRequest;
import com.example.airlineapi.booking.dto.BookingDtos.BookingResponse;
import com.example.airlineapi.booking.event.BookingPaidEvent;
import com.example.airlineapi.common.ApiException;
import com.example.airlineapi.flight.Flight;
import com.example.airlineapi.flight.FlightService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.context.ApplicationEventPublisher;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class BookingServiceTest {

    private BookingRepository bookingRepository;
    private FlightService flightService;
    private ApplicationEventPublisher eventPublisher;
    private BookingService bookingService;

    @BeforeEach
    void setUp() {
        bookingRepository = mock(BookingRepository.class);
        flightService = mock(FlightService.class);
        eventPublisher = mock(ApplicationEventPublisher.class);
        bookingService = new BookingService(bookingRepository, flightService, eventPublisher);
    }

    @Test
    void create_rejectsWhenSeatsInsufficient() {
        Flight flight = new Flight("UA100", "ORD", "SFO", Instant.now(), BigDecimal.TEN, 2);
        when(flightService.findOrThrow(1L)).thenReturn(flight);

        BookingRequest request = new BookingRequest(List.of(new BookingLineRequest(1L, 5)));

        assertThatThrownBy(() -> bookingService.create("alice", request))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Not enough seats");
    }

    @Test
    void create_decrementsSeatsAndPublishesEvent() {
        Flight flight = new Flight("UA200", "ORD", "SFO", Instant.now(), BigDecimal.valueOf(20), 10);
        when(flightService.findOrThrow(2L)).thenReturn(flight);

        BookingRequest request = new BookingRequest(List.of(new BookingLineRequest(2L, 3)));

        BookingResponse response = bookingService.create("bob", request);

        assertThat(flight.getSeatCapacity()).isEqualTo(7);
        assertThat(response.totalFare()).isEqualByComparingTo("60");
        verify(eventPublisher).publishEvent(any());
    }

    @Test
    void pay_publishesBookingPaidEvent() {
        Booking booking = new Booking("bob");
        booking.addItem(new BookingItem(1L, 2, BigDecimal.valueOf(30)));
        when(bookingRepository.findById(7L)).thenReturn(Optional.of(booking));

        bookingService.pay("bob", false, 7L);

        verify(eventPublisher).publishEvent(any(BookingPaidEvent.class));
    }

    @Test
    void cancel_releasesSeatsWhetherPendingOrPaid() {
        Flight flight = new Flight("UA300", "ORD", "SFO", Instant.now(), BigDecimal.TEN, 10);
        flight.decreaseSeats(4); // simulate seats already reserved at booking-creation time
        when(flightService.findOrThrow(3L)).thenReturn(flight);

        Booking booking = new Booking("carl");
        booking.addItem(new BookingItem(3L, 4, BigDecimal.TEN));
        when(bookingRepository.findById(42L)).thenReturn(Optional.of(booking));

        bookingService.cancel("carl", false, 42L);

        assertThat(flight.getSeatCapacity()).isEqualTo(10);
        assertThat(booking.getStatus()).isEqualTo(BookingStatus.CANCELLED);
    }

    @Test
    void ticketDirectlyFromPending_isRejected() {
        Booking booking = new Booking("dave");
        when(bookingRepository.findById(9L)).thenReturn(Optional.of(booking));

        assertThatThrownBy(() -> bookingService.ticket(9L))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Cannot transition");
    }
}
