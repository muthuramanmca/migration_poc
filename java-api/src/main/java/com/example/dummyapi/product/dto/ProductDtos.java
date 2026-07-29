package com.example.dummyapi.product.dto;

import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;

import java.math.BigDecimal;

public class ProductDtos {

    public record ProductRequest(
            @NotBlank String sku,
            @NotBlank String name,
            String description,
            @DecimalMin(value = "0.01", message = "Price must be greater than zero") BigDecimal price,
            @Min(value = 0, message = "Initial stock cannot be negative") int stockQuantity
    ) {}

    public record StockAdjustmentRequest(int delta) {}

    public record ProductResponse(
            Long id,
            String sku,
            String name,
            String description,
            BigDecimal price,
            int stockQuantity,
            boolean lowStock
    ) {}
}
