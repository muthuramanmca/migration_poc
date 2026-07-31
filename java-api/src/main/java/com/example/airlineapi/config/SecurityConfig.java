package com.example.airlineapi.config;

import com.example.airlineapi.security.JwtAuthFilter;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.annotation.web.configurers.AbstractHttpConfigurer;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.authentication.UsernamePasswordAuthenticationFilter;

@Configuration
public class SecurityConfig {

    private final JwtAuthFilter jwtAuthFilter;

    public SecurityConfig(JwtAuthFilter jwtAuthFilter) {
        this.jwtAuthFilter = jwtAuthFilter;
    }

    @Bean
    public PasswordEncoder passwordEncoder() {
        return new BCryptPasswordEncoder();
    }

    @Bean
    public SecurityFilterChain securityFilterChain(HttpSecurity http) throws Exception {
        http
            .csrf(AbstractHttpConfigurer::disable)
            .sessionManagement(sm -> sm.sessionCreationPolicy(SessionCreationPolicy.STATELESS))
            .authorizeHttpRequests(auth -> auth
                // Public: registration, login, browsing the flight schedule, and the API contract itself
                .requestMatchers("/api/auth/**").permitAll()
                .requestMatchers("GET", "/api/flights/**").permitAll()
                .requestMatchers("/v3/api-docs/**", "/swagger-ui/**", "/swagger-ui.html", "/h2-console/**").permitAll()
                // Admin-only: schedule and fulfilment mutations
                .requestMatchers("POST", "/api/flights/**").hasRole("ADMIN")
                .requestMatchers("PUT", "/api/flights/**").hasRole("ADMIN")
                .requestMatchers("DELETE", "/api/flights/**").hasRole("ADMIN")
                .requestMatchers("POST", "/api/bookings/*/ticket").hasRole("ADMIN")
                .requestMatchers("POST", "/api/bookings/*/complete").hasRole("ADMIN")
                .requestMatchers("GET", "/api/notifications").hasRole("ADMIN")
                // Loyalty: self-service routes must be matched before the general Admin rule below
                .requestMatchers("GET", "/api/loyalty/me").authenticated()
                .requestMatchers("POST", "/api/loyalty/redeem").authenticated()
                .requestMatchers("GET", "/api/loyalty/*").hasRole("ADMIN")
                // Everything else requires a logged-in user
                .anyRequest().authenticated()
            )
            .headers(headers -> headers.frameOptions(frame -> frame.disable())) // needed for /h2-console
            .addFilterBefore(jwtAuthFilter, UsernamePasswordAuthenticationFilter.class);

        return http.build();
    }
}
