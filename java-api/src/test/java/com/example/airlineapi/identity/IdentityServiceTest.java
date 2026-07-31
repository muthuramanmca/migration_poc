package com.example.airlineapi.identity;

import com.example.airlineapi.common.ApiException;
import com.example.airlineapi.identity.dto.IdentityDtos.LoginRequest;
import com.example.airlineapi.identity.dto.IdentityDtos.PassengerResponse;
import com.example.airlineapi.identity.dto.IdentityDtos.RegisterRequest;
import com.example.airlineapi.identity.event.PassengerRegisteredEvent;
import com.example.airlineapi.security.JwtService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;
import org.springframework.security.crypto.password.PasswordEncoder;

import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class IdentityServiceTest {

    private PassengerRepository passengerRepository;
    private PasswordEncoder passwordEncoder;
    private JwtService jwtService;
    private ApplicationEventPublisher eventPublisher;
    private IdentityService identityService;

    @BeforeEach
    void setUp() {
        passengerRepository = mock(PassengerRepository.class);
        passwordEncoder = new BCryptPasswordEncoder();
        jwtService = mock(JwtService.class);
        eventPublisher = mock(ApplicationEventPublisher.class);
        identityService = new IdentityService(passengerRepository, passwordEncoder, jwtService, eventPublisher);
    }

    @Test
    void register_rejectsDuplicateUsername() {
        when(passengerRepository.existsByUsername("alice")).thenReturn(true);

        RegisterRequest request = new RegisterRequest("alice", "alice@example.com", "password1");

        assertThatThrownBy(() -> identityService.register(request))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Username");
    }

    @Test
    void register_rejectsDuplicateEmail() {
        when(passengerRepository.existsByUsername("alice")).thenReturn(false);
        when(passengerRepository.existsByEmail("alice@example.com")).thenReturn(true);

        RegisterRequest request = new RegisterRequest("alice", "alice@example.com", "password1");

        assertThatThrownBy(() -> identityService.register(request))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Email");
    }

    @Test
    void register_newPassengerDefaultsToPassengerRole() {
        RegisterRequest request = new RegisterRequest("bob", "bob@example.com", "password1");

        PassengerResponse response = identityService.register(request);

        assertThat(response.role()).isEqualTo(Role.PASSENGER);
        verify(passengerRepository).save(any(Passenger.class));
    }

    @Test
    void register_publishesPassengerRegisteredEvent() {
        RegisterRequest request = new RegisterRequest("dave", "dave@example.com", "password1");

        identityService.register(request);

        verify(eventPublisher).publishEvent(any(PassengerRegisteredEvent.class));
    }

    @Test
    void login_rejectsUnknownUsername() {
        when(passengerRepository.findByUsername("ghost")).thenReturn(Optional.empty());

        assertThatThrownBy(() -> identityService.login(new LoginRequest("ghost", "whatever1")))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Invalid username or password");
    }

    @Test
    void login_rejectsWrongPassword() {
        Passenger existing = new Passenger("carl", "carl@example.com", passwordEncoder.encode("correct1"), Role.PASSENGER);
        when(passengerRepository.findByUsername("carl")).thenReturn(Optional.of(existing));

        assertThatThrownBy(() -> identityService.login(new LoginRequest("carl", "wrong1")))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Invalid username or password");
    }
}
