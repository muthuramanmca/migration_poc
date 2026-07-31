package com.example.airlineapi.notification;

import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.assertj.core.api.Assertions.assertThat;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class NotificationServiceTest {

    private NotificationLogRepository notificationLogRepository;
    private NotificationService notificationService;

    @BeforeEach
    void setUp() {
        notificationLogRepository = mock(NotificationLogRepository.class);
        notificationService = new NotificationService(notificationLogRepository);
    }

    @Test
    void send_persistsALogEntry() {
        notificationService.send(1L, "BOOKING_CREATED");

        verify(notificationLogRepository).save(any(NotificationLog.class));
    }

    @Test
    void listAll_returnsWhatTheRepositoryHas() {
        NotificationLog log = new NotificationLog(1L, "BOOKING_CREATED");
        when(notificationLogRepository.findAll()).thenReturn(List.of(log));

        List<NotificationLog> result = notificationService.listAll();

        assertThat(result).containsExactly(log);
    }
}
