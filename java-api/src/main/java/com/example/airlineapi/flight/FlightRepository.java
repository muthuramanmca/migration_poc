package com.example.airlineapi.flight;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;
import java.util.Optional;

public interface FlightRepository extends JpaRepository<Flight, Long> {
    boolean existsByFlightNumber(String flightNumber);
    Optional<Flight> findByFlightNumber(String flightNumber);
    List<Flight> findAllByActiveTrue();
    Optional<Flight> findByIdAndActiveTrue(Long id);
}
