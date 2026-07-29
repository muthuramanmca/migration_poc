package com.example.dummyapi.common;

import java.time.Instant;
import java.util.List;

public record ApiError(
        Instant timestamp,
        int status,
        String errorCode,
        String message,
        List<String> fieldErrors
) {
    public static ApiError of(int status, String errorCode, String message) {
        return new ApiError(Instant.now(), status, errorCode, message, List.of());
    }

    public static ApiError of(int status, String errorCode, String message, List<String> fieldErrors) {
        return new ApiError(Instant.now(), status, errorCode, message, fieldErrors);
    }
}
