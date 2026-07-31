package com.example.airlineapi.identity;

import com.example.airlineapi.identity.dto.IdentityDtos.AuthResponse;
import com.example.airlineapi.identity.dto.IdentityDtos.LoginRequest;
import com.example.airlineapi.identity.dto.IdentityDtos.PassengerResponse;
import com.example.airlineapi.identity.dto.IdentityDtos.RegisterRequest;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/auth")
public class AuthController {

    private final IdentityService identityService;

    public AuthController(IdentityService identityService) {
        this.identityService = identityService;
    }

    @PostMapping("/register")
    public ResponseEntity<PassengerResponse> register(@Valid @RequestBody RegisterRequest request) {
        return ResponseEntity.status(201).body(identityService.register(request));
    }

    @PostMapping("/login")
    public AuthResponse login(@Valid @RequestBody LoginRequest request) {
        return identityService.login(request);
    }
}
