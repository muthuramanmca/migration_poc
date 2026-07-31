package com.example.airlineapi.flight;

import com.example.airlineapi.common.ApiException;
import com.example.airlineapi.flight.dto.FlightDtos.FlightRequest;
import com.example.airlineapi.flight.dto.FlightDtos.FlightResponse;
import com.example.airlineapi.flight.dto.FlightDtos.SeatAdjustmentRequest;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

@Service
public class FlightService {

    private final FlightRepository flightRepository;

    // Config-driven business rule: the low-seat threshold is not hardcoded,
    // it comes from application.yml (app.flights.low-seat-threshold).
    @Value("${app.flights.low-seat-threshold}")
    private int lowSeatThreshold;

    public FlightService(FlightRepository flightRepository) {
        this.flightRepository = flightRepository;
    }

    @Transactional
    public FlightResponse create(FlightRequest request) {
        if (flightRepository.existsByFlightNumber(request.flightNumber())) {
            throw ApiException.conflict("DUPLICATE_FLIGHT_NUMBER", "A flight with this number already exists");
        }
        Flight flight = new Flight(
                request.flightNumber(), request.origin(), request.destination(),
                request.departureAt(), request.fare(), request.seatCapacity()
        );
        flight.setLowSeatThreshold(lowSeatThreshold);
        flightRepository.save(flight);
        return toResponse(flight);
    }

    public FlightResponse getById(Long id) {
        return toResponse(findOrThrow(id));
    }

    public List<FlightResponse> listAll() {
        return flightRepository.findAllByActiveTrue().stream().map(this::toResponse).toList();
    }

    @Transactional
    public FlightResponse adjustSeats(Long id, SeatAdjustmentRequest request) {
        Flight flight = findOrThrow(id);
        int resultingSeats = flight.getSeatCapacity() + request.delta();
        if (resultingSeats < 0) {
            throw ApiException.conflict("INSUFFICIENT_SEATS",
                    "Seat adjustment would result in a negative capacity for flight " + flight.getFlightNumber());
        }
        if (request.delta() >= 0) {
            flight.increaseSeats(request.delta());
        } else {
            flight.decreaseSeats(-request.delta());
        }
        return toResponse(flight);
    }

    @Transactional
    public void delete(Long id) {
        Flight flight = findOrThrow(id);
        flight.deactivate();
    }

    /**
     * Public: used by BookingService (a different package) to reserve/release seats within a
     * transaction. Excludes deactivated flights, so a cancelled schedule is 404 everywhere,
     * including new booking creation.
     */
    public Flight findOrThrow(Long id) {
        Flight flight = flightRepository.findByIdAndActiveTrue(id)
                .orElseThrow(() -> ApiException.notFound("FLIGHT_NOT_FOUND", "Flight not found: " + id));
        flight.setLowSeatThreshold(lowSeatThreshold);
        return flight;
    }

    private FlightResponse toResponse(Flight flight) {
        return new FlightResponse(
                flight.getId(), flight.getFlightNumber(), flight.getOrigin(), flight.getDestination(),
                flight.getDepartureAt(), flight.getFare(), flight.getSeatCapacity(), flight.isLowSeatAvailability()
        );
    }
}
