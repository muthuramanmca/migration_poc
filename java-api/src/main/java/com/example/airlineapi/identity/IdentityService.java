package com.example.airlineapi.identity;

import com.example.airlineapi.common.ApiException;
import com.example.airlineapi.identity.dto.IdentityDtos.AuthResponse;
import com.example.airlineapi.identity.dto.IdentityDtos.LoginRequest;
import com.example.airlineapi.identity.dto.IdentityDtos.PassengerResponse;
import com.example.airlineapi.identity.dto.IdentityDtos.RegisterRequest;
import com.example.airlineapi.identity.event.PassengerRegisteredEvent;
import com.example.airlineapi.security.JwtService;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class IdentityService {

    private final PassengerRepository passengerRepository;
    private final PasswordEncoder passwordEncoder;
    private final JwtService jwtService;
    private final ApplicationEventPublisher eventPublisher;

    public IdentityService(PassengerRepository passengerRepository, PasswordEncoder passwordEncoder,
                            JwtService jwtService, ApplicationEventPublisher eventPublisher) {
        this.passengerRepository = passengerRepository;
        this.passwordEncoder = passwordEncoder;
        this.jwtService = jwtService;
        this.eventPublisher = eventPublisher;
    }

    @Transactional
    public PassengerResponse register(RegisterRequest request) {
        if (passengerRepository.existsByUsername(request.username())) {
            throw ApiException.conflict("DUPLICATE_USERNAME", "Username is already taken");
        }
        if (passengerRepository.existsByEmail(request.email())) {
            throw ApiException.conflict("DUPLICATE_EMAIL", "Email is already registered");
        }

        // New registrations always start as PASSENGER; only an existing ADMIN could
        // promote someone later (not exposed in this demo app). The rule that
        // "role is never client-supplied at signup" matters for the .NET rewrite.
        Passenger passenger = new Passenger(
                request.username(),
                request.email(),
                passwordEncoder.encode(request.password()),
                Role.PASSENGER
        );
        passengerRepository.save(passenger);

        // Fired inside the open transaction, before commit -- deliberately, same
        // antipattern as booking's events. Consumed by loyalty to auto-create the
        // LoyaltyAccount; identity has no direct dependency on the loyalty package.
        eventPublisher.publishEvent(new PassengerRegisteredEvent(this, passenger.getUsername()));

        return toResponse(passenger);
    }

    public AuthResponse login(LoginRequest request) {
        Passenger passenger = passengerRepository.findByUsername(request.username())
                .orElseThrow(() -> ApiException.unauthorized("INVALID_CREDENTIALS", "Invalid username or password"));

        if (!passwordEncoder.matches(request.password(), passenger.getPasswordHash())) {
            throw ApiException.unauthorized("INVALID_CREDENTIALS", "Invalid username or password");
        }

        String token = jwtService.generateToken(passenger.getUsername(), passenger.getRole().name());
        return new AuthResponse(token, JwtService.EXPIRY_SECONDS);
    }

    public PassengerResponse getByUsername(String username) {
        Passenger passenger = passengerRepository.findByUsername(username)
                .orElseThrow(() -> ApiException.notFound("PASSENGER_NOT_FOUND", "Passenger not found: " + username));
        return toResponse(passenger);
    }

    private PassengerResponse toResponse(Passenger passenger) {
        return new PassengerResponse(passenger.getId(), passenger.getUsername(), passenger.getEmail(), passenger.getRole());
    }
}
