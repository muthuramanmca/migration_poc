package com.example.airlineapi.identity;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.Optional;

public interface PassengerRepository extends JpaRepository<Passenger, Long> {
    Optional<Passenger> findByUsername(String username);
    boolean existsByUsername(String username);
    boolean existsByEmail(String email);
}
