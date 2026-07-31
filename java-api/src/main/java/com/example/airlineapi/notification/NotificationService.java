package com.example.airlineapi.notification;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

/**
 * Deliberately naive/synchronous: called directly from booking.event.BookingEventListener inside
 * the open transaction, no real email/SMS provider integration. This is exactly what dotnet-api's
 * real MassTransit consumer (Notification service, wired to an outbox-published event) replaces --
 * the motivating "before" state for that migration.
 */
@Service
public class NotificationService {

    private static final Logger log = LoggerFactory.getLogger(NotificationService.class);

    private final NotificationLogRepository notificationLogRepository;

    public NotificationService(NotificationLogRepository notificationLogRepository) {
        this.notificationLogRepository = notificationLogRepository;
    }

    @Transactional
    public void send(Long bookingId, String type) {
        log.info("Booking {} -- {} -- would email passenger here", bookingId, type);
        notificationLogRepository.save(new NotificationLog(bookingId, type));
    }

    public List<NotificationLog> listAll() {
        return notificationLogRepository.findAll();
    }
}
