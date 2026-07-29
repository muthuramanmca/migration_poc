package com.example.dummyapi.product;

import com.example.dummyapi.common.ApiException;
import com.example.dummyapi.product.dto.ProductDtos.ProductRequest;
import com.example.dummyapi.product.dto.ProductDtos.ProductResponse;
import com.example.dummyapi.product.dto.ProductDtos.StockAdjustmentRequest;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.test.util.ReflectionTestUtils;

import java.math.BigDecimal;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class ProductServiceTest {

    private ProductRepository productRepository;
    private ProductService productService;

    @BeforeEach
    void setUp() {
        productRepository = mock(ProductRepository.class);
        productService = new ProductService(productRepository);
        ReflectionTestUtils.setField(productService, "lowStockThreshold", 10);
    }

    @Test
    void create_rejectsDuplicateSku() {
        when(productRepository.existsBySku("SKU-1")).thenReturn(true);

        ProductRequest request = new ProductRequest("SKU-1", "Widget", "desc", BigDecimal.TEN, 5);

        assertThatThrownBy(() -> productService.create(request))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("SKU");
    }

    @Test
    void create_flagsLowStockWhenBelowThreshold() {
        ProductRequest request = new ProductRequest("SKU-2", "Gadget", "desc", BigDecimal.TEN, 3);

        ProductResponse response = productService.create(request);

        assertThat(response.lowStock()).isTrue();
        assertThat(response.stockQuantity()).isEqualTo(3);
    }

    @Test
    void create_doesNotFlagLowStockAboveThreshold() {
        ProductRequest request = new ProductRequest("SKU-3", "Gizmo", "desc", BigDecimal.TEN, 50);

        ProductResponse response = productService.create(request);

        assertThat(response.lowStock()).isFalse();
    }

    @Test
    void adjustStock_rejectsNegativeResultingQuantity() {
        Product product = new Product("SKU-4", "Doohickey", "desc", BigDecimal.TEN, 5);
        when(productRepository.findByIdAndActiveTrue(1L)).thenReturn(Optional.of(product));

        assertThatThrownBy(() -> productService.adjustStock(1L, new StockAdjustmentRequest(-10)))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("negative");
    }

    @Test
    void delete_deactivatesProductInsteadOfRemovingIt() {
        Product product = new Product("SKU-5", "Thingamajig", "desc", BigDecimal.TEN, 5);
        when(productRepository.findByIdAndActiveTrue(1L)).thenReturn(Optional.of(product));

        productService.delete(1L);

        assertThat(product.isActive()).isFalse();
    }

    @Test
    void delete_throwsNotFoundWhenAlreadyDeactivated() {
        when(productRepository.findByIdAndActiveTrue(1L)).thenReturn(Optional.empty());

        assertThatThrownBy(() -> productService.delete(1L))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("not found");
    }

    @Test
    void findOrThrow_excludesDeactivatedProducts() {
        when(productRepository.findByIdAndActiveTrue(1L)).thenReturn(Optional.empty());

        assertThatThrownBy(() -> productService.getById(1L))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("not found");
    }
}
