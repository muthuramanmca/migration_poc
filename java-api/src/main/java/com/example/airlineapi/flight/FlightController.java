package com.example.airlineapi.flight;

import com.example.airlineapi.flight.dto.FlightDtos.FlightRequest;
import com.example.airlineapi.flight.dto.FlightDtos.FlightResponse;
import com.example.airlineapi.flight.dto.FlightDtos.SeatAdjustmentRequest;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.DeleteMapping;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@RestController
@RequestMapping("/api/flights")
public class FlightController {

    private final FlightService flightService;

    public FlightController(FlightService flightService) {
        this.flightService = flightService;
    }

    @GetMapping
    public List<FlightResponse> list() {
        return flightService.listAll();
    }

    @GetMapping("/{id}")
    public FlightResponse get(@PathVariable Long id) {
        return flightService.getById(id);
    }

    @PostMapping
    public ResponseEntity<FlightResponse> create(@Valid @RequestBody FlightRequest request) {
        return ResponseEntity.status(201).body(flightService.create(request));
    }

    @PutMapping("/{id}/seats")
    public FlightResponse adjustSeats(@PathVariable Long id, @RequestBody SeatAdjustmentRequest request) {
        return flightService.adjustSeats(id, request);
    }

    @DeleteMapping("/{id}")
    public ResponseEntity<Void> delete(@PathVariable Long id) {
        flightService.delete(id);
        return ResponseEntity.noContent().build();
    }
}
