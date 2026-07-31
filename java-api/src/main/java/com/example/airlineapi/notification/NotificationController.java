package com.example.airlineapi.notification;

import com.example.airlineapi.notification.dto.NotificationDtos.NotificationLogResponse;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@RestController
@RequestMapping("/api/notifications")
public class NotificationController {

    private final NotificationService notificationService;

    public NotificationController(NotificationService notificationService) {
        this.notificationService = notificationService;
    }

    @GetMapping
    public List<NotificationLogResponse> list() {
        return notificationService.listAll().stream()
                .map(n -> new NotificationLogResponse(n.getId(), n.getBookingId(), n.getType(), n.getSentAt()))
                .toList();
    }
}
