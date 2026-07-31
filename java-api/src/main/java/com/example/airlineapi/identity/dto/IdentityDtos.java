package com.example.airlineapi.identity.dto;

import com.example.airlineapi.common.ValidPassword;
import com.example.airlineapi.identity.Role;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public class IdentityDtos {

    public record RegisterRequest(
            @NotBlank @Size(min = 3, max = 30) String username,
            @NotBlank @Email String email,
            @ValidPassword String password
    ) {}

    public record LoginRequest(
            @NotBlank String username,
            @NotBlank String password
    ) {}

    public record AuthResponse(String token, long expiresInSeconds) {}

    public record PassengerResponse(Long id, String username, String email, Role role) {}
}
