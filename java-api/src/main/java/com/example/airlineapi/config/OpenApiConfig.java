package com.example.airlineapi.config;

import io.swagger.v3.oas.annotations.OpenAPIDefinition;
import io.swagger.v3.oas.annotations.enums.SecuritySchemeType;
import io.swagger.v3.oas.annotations.info.Info;
import io.swagger.v3.oas.annotations.security.SecurityScheme;
import org.springframework.context.annotation.Configuration;

@Configuration
@OpenAPIDefinition(
        info = @Info(
                title = "Java API",
                version = "0.1.0",
                description = "Airline ticket-booking API (Identity, Flights, Bookings, Notifications, " +
                        "Loyalty) used as the source application for the Java -> .NET Core migration " +
                        "exercise. Export the contract at /v3/api-docs (Phase 2a)."
        )
)
@SecurityScheme(
        name = "bearerAuth",
        type = SecuritySchemeType.HTTP,
        scheme = "bearer",
        bearerFormat = "JWT"
)
public class OpenApiConfig {
}
