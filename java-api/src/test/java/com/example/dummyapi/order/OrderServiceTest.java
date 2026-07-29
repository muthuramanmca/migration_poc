package com.example.dummyapi.order;

import com.example.dummyapi.common.ApiException;
import com.example.dummyapi.order.dto.OrderDtos.OrderLineRequest;
import com.example.dummyapi.order.dto.OrderDtos.OrderRequest;
import com.example.dummyapi.order.dto.OrderDtos.OrderResponse;
import com.example.dummyapi.product.Product;
import com.example.dummyapi.product.ProductService;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.context.ApplicationEventPublisher;

import java.math.BigDecimal;
import java.util.List;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class OrderServiceTest {

    private OrderRepository orderRepository;
    private ProductService productService;
    private ApplicationEventPublisher eventPublisher;
    private OrderService orderService;

    @BeforeEach
    void setUp() {
        orderRepository = mock(OrderRepository.class);
        productService = mock(ProductService.class);
        eventPublisher = mock(ApplicationEventPublisher.class);
        orderService = new OrderService(orderRepository, productService, eventPublisher);
    }

    @Test
    void create_rejectsWhenStockInsufficient() {
        Product product = new Product("SKU-1", "Widget", "desc", BigDecimal.TEN, 2);
        when(productService.findOrThrow(1L)).thenReturn(product);

        OrderRequest request = new OrderRequest(List.of(new OrderLineRequest(1L, 5)));

        assertThatThrownBy(() -> orderService.create("alice", request))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Not enough stock");
    }

    @Test
    void create_decrementsStockAndPublishesEvent() {
        Product product = new Product("SKU-2", "Gadget", "desc", BigDecimal.valueOf(20), 10);
        when(productService.findOrThrow(2L)).thenReturn(product);

        OrderRequest request = new OrderRequest(List.of(new OrderLineRequest(2L, 3)));

        OrderResponse response = orderService.create("bob", request);

        assertThat(product.getStockQuantity()).isEqualTo(7);
        assertThat(response.totalAmount()).isEqualByComparingTo("60");
        verify(eventPublisher).publishEvent(any());
    }

    @Test
    void cancel_restocksItemsWhetherPendingOrPaid() {
        Product product = new Product("SKU-3", "Gizmo", "desc", BigDecimal.TEN, 10);
        product.decreaseStock(4); // simulate stock already reserved at order-creation time
        when(productService.findOrThrow(3L)).thenReturn(product);

        Order order = new Order("carl");
        order.addItem(new OrderItem(3L, 4, BigDecimal.TEN));
        when(orderRepository.findById(42L)).thenReturn(Optional.of(order));

        orderService.cancel("carl", false, 42L);

        assertThat(product.getStockQuantity()).isEqualTo(10);
        assertThat(order.getStatus()).isEqualTo(OrderStatus.CANCELLED);
    }

    @Test
    void shipDirectlyFromPending_isRejected() {
        Order order = new Order("dave");
        when(orderRepository.findById(9L)).thenReturn(Optional.of(order));

        assertThatThrownBy(() -> orderService.ship(9L))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Cannot transition");
    }
}
