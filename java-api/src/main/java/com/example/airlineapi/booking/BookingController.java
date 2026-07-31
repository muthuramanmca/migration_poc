package com.example.airlineapi.booking;

import com.example.airlineapi.booking.dto.BookingDtos.BookingRequest;
import com.example.airlineapi.booking.dto.BookingDtos.BookingResponse;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.Authentication;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@RestController
@RequestMapping("/api/bookings")
public class BookingController {

    private final BookingService bookingService;

    public BookingController(BookingService bookingService) {
        this.bookingService = bookingService;
    }

    @PostMapping
    public ResponseEntity<BookingResponse> create(Authentication auth, @Valid @RequestBody BookingRequest request) {
        return ResponseEntity.status(201).body(bookingService.create(auth.getName(), request));
    }

    @GetMapping
    public List<BookingResponse> list(Authentication auth) {
        return bookingService.listForUser(auth.getName(), isAdmin(auth));
    }

    @GetMapping("/{id}")
    public BookingResponse get(Authentication auth, @PathVariable Long id) {
        return bookingService.getById(auth.getName(), isAdmin(auth), id);
    }

    @PostMapping("/{id}/pay")
    public BookingResponse pay(Authentication auth, @PathVariable Long id) {
        return bookingService.pay(auth.getName(), isAdmin(auth), id);
    }

    @PostMapping("/{id}/ticket")
    public BookingResponse ticket(@PathVariable Long id) {
        return bookingService.ticket(id);
    }

    @PostMapping("/{id}/complete")
    public BookingResponse complete(@PathVariable Long id) {
        return bookingService.complete(id);
    }

    @PostMapping("/{id}/cancel")
    public BookingResponse cancel(Authentication auth, @PathVariable Long id) {
        return bookingService.cancel(auth.getName(), isAdmin(auth), id);
    }

    private boolean isAdmin(Authentication auth) {
        return auth.getAuthorities().stream().anyMatch(a -> a.getAuthority().equals("ROLE_ADMIN"));
    }
}
