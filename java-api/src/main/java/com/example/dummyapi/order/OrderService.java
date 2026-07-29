package com.example.dummyapi.order;

import com.example.dummyapi.common.ApiException;
import com.example.dummyapi.order.dto.OrderDtos.OrderItemResponse;
import com.example.dummyapi.order.dto.OrderDtos.OrderLineRequest;
import com.example.dummyapi.order.dto.OrderDtos.OrderRequest;
import com.example.dummyapi.order.dto.OrderDtos.OrderResponse;
import com.example.dummyapi.order.event.OrderCreatedEvent;
import com.example.dummyapi.product.Product;
import com.example.dummyapi.product.ProductService;
import org.springframework.context.ApplicationEventPublisher;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

@Service
public class OrderService {

    private final OrderRepository orderRepository;
    private final ProductService productService;
    private final ApplicationEventPublisher eventPublisher;

    public OrderService(OrderRepository orderRepository, ProductService productService,
                         ApplicationEventPublisher eventPublisher) {
        this.orderRepository = orderRepository;
        this.productService = productService;
        this.eventPublisher = eventPublisher;
    }

    @Transactional
    public OrderResponse create(String username, OrderRequest request) {
        Order order = new Order(username);

        for (OrderLineRequest line : request.items()) {
            Product product = productService.findOrThrow(line.productId());

            if (product.getStockQuantity() < line.quantity()) {
                throw ApiException.conflict("INSUFFICIENT_STOCK",
                        "Not enough stock for product " + product.getSku() +
                                " (requested " + line.quantity() + ", available " + product.getStockQuantity() + ")");
            }

            product.decreaseStock(line.quantity());
            order.addItem(new OrderItem(product.getId(), line.quantity(), product.getPrice()));
        }

        orderRepository.save(order);
        eventPublisher.publishEvent(new OrderCreatedEvent(this, order.getId(), username));
        return toResponse(order);
    }

    public OrderResponse getById(String requester, boolean isAdmin, Long id) {
        Order order = findOrThrow(id);
        assertOwnerOrAdmin(requester, isAdmin, order);
        return toResponse(order);
    }

    public List<OrderResponse> listForUser(String requester, boolean isAdmin) {
        // Admins see every order; regular users only ever see their own -
        // enforced here rather than trusting a query parameter.
        List<Order> orders = isAdmin ? orderRepository.findAll() : orderRepository.findByUsername(requester);
        return orders.stream().map(this::toResponse).toList();
    }

    @Transactional
    public OrderResponse pay(String requester, boolean isAdmin, Long id) {
        Order order = findOrThrow(id);
        assertOwnerOrAdmin(requester, isAdmin, order);
        order.transitionTo(OrderStatus.PAID);
        return toResponse(order);
    }

    @Transactional
    public OrderResponse ship(Long id) {
        Order order = findOrThrow(id);
        order.transitionTo(OrderStatus.SHIPPED);
        return toResponse(order);
    }

    @Transactional
    public OrderResponse deliver(Long id) {
        Order order = findOrThrow(id);
        order.transitionTo(OrderStatus.DELIVERED);
        return toResponse(order);
    }

    @Transactional
    public OrderResponse cancel(String requester, boolean isAdmin, Long id) {
        Order order = findOrThrow(id);
        assertOwnerOrAdmin(requester, isAdmin, order);

        order.transitionTo(OrderStatus.CANCELLED);

        // Stock is reserved (decremented) at order-creation time, before any
        // payment step exists - so cancelling from PENDING restocks exactly the
        // same way cancelling from PAID does. Easy to get wrong if you assume
        // restocking only applies to already-paid orders.
        for (OrderItem item : order.getItems()) {
            Product product = productService.findOrThrow(item.getProductId());
            product.increaseStock(item.getQuantity());
        }

        return toResponse(order);
    }

    private void assertOwnerOrAdmin(String requester, boolean isAdmin, Order order) {
        if (!isAdmin && !order.getUsername().equals(requester)) {
            throw new AccessDeniedException("Not allowed to access this order");
        }
    }

    private Order findOrThrow(Long id) {
        return orderRepository.findById(id)
                .orElseThrow(() -> ApiException.notFound("ORDER_NOT_FOUND", "Order not found: " + id));
    }

    private OrderResponse toResponse(Order order) {
        List<OrderItemResponse> items = order.getItems().stream()
                .map(i -> new OrderItemResponse(i.getProductId(), i.getQuantity(), i.getUnitPriceAtPurchase(), i.lineTotal()))
                .toList();
        return new OrderResponse(order.getId(), order.getUsername(), order.getStatus().name(),
                order.getTotalAmount(), order.getCreatedAt(), items);
    }
}
