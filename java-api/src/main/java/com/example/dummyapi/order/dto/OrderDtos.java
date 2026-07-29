package com.example.dummyapi.order.dto;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;

public class OrderDtos {

    public record OrderLineRequest(
            @NotNull Long productId,
            @Min(1) int quantity
    ) {}

    public record OrderRequest(
            @NotEmpty @Valid List<OrderLineRequest> items
    ) {}

    public record OrderItemResponse(Long productId, int quantity, BigDecimal unitPriceAtPurchase, BigDecimal lineTotal) {}

    public record OrderResponse(
            Long id,
            String username,
            String status,
            BigDecimal totalAmount,
            Instant createdAt,
            List<OrderItemResponse> items
    ) {}
}
