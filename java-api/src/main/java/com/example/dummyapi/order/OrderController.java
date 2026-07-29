package com.example.dummyapi.order;

import com.example.dummyapi.order.dto.OrderDtos.OrderRequest;
import com.example.dummyapi.order.dto.OrderDtos.OrderResponse;
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
@RequestMapping("/api/orders")
public class OrderController {

    private final OrderService orderService;

    public OrderController(OrderService orderService) {
        this.orderService = orderService;
    }

    @PostMapping
    public ResponseEntity<OrderResponse> create(Authentication auth, @Valid @RequestBody OrderRequest request) {
        return ResponseEntity.status(201).body(orderService.create(auth.getName(), request));
    }

    @GetMapping
    public List<OrderResponse> list(Authentication auth) {
        return orderService.listForUser(auth.getName(), isAdmin(auth));
    }

    @GetMapping("/{id}")
    public OrderResponse get(Authentication auth, @PathVariable Long id) {
        return orderService.getById(auth.getName(), isAdmin(auth), id);
    }

    @PostMapping("/{id}/pay")
    public OrderResponse pay(Authentication auth, @PathVariable Long id) {
        return orderService.pay(auth.getName(), isAdmin(auth), id);
    }

    @PostMapping("/{id}/ship")
    public OrderResponse ship(@PathVariable Long id) {
        return orderService.ship(id);
    }

    @PostMapping("/{id}/deliver")
    public OrderResponse deliver(@PathVariable Long id) {
        return orderService.deliver(id);
    }

    @PostMapping("/{id}/cancel")
    public OrderResponse cancel(Authentication auth, @PathVariable Long id) {
        return orderService.cancel(auth.getName(), isAdmin(auth), id);
    }

    private boolean isAdmin(Authentication auth) {
        return auth.getAuthorities().stream().anyMatch(a -> a.getAuthority().equals("ROLE_ADMIN"));
    }
}
