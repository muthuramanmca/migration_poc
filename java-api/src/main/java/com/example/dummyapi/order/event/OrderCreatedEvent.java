package com.example.dummyapi.order.event;

import org.springframework.context.ApplicationEvent;

/**
 * Represents the integration point every migrated module needs a plan for:
 * in the real Java app this might publish to Kafka/RabbitMQ instead of an
 * in-process Spring event. On the .NET side this is the seam where you'd
 * decide between MassTransit + a broker, or an equivalent in-process mechanism.
 */
public class OrderCreatedEvent extends ApplicationEvent {
    private final Long orderId;
    private final String username;

    public OrderCreatedEvent(Object source, Long orderId, String username) {
        super(source);
        this.orderId = orderId;
        this.username = username;
    }

    public Long getOrderId() { return orderId; }
    public String getUsername() { return username; }
}
