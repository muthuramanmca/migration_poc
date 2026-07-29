package com.example.dummyapi.common;

import org.springframework.http.HttpStatus;

/**
 * Single exception type carrying the HTTP status it should map to.
 * GlobalExceptionHandler reads status/errorCode straight off the exception,
 * which is why the mapping between "business rule violated" and "HTTP response"
 * lives in one place (here + GlobalExceptionHandler) rather than scattered
 * across every controller.
 */
public class ApiException extends RuntimeException {

    private final HttpStatus status;
    private final String errorCode;

    private ApiException(HttpStatus status, String errorCode, String message) {
        super(message);
        this.status = status;
        this.errorCode = errorCode;
    }

    public static ApiException notFound(String errorCode, String message) {
        return new ApiException(HttpStatus.NOT_FOUND, errorCode, message);
    }

    public static ApiException conflict(String errorCode, String message) {
        return new ApiException(HttpStatus.CONFLICT, errorCode, message);
    }

    public static ApiException unauthorized(String errorCode, String message) {
        return new ApiException(HttpStatus.UNAUTHORIZED, errorCode, message);
    }

    public static ApiException badRequest(String errorCode, String message) {
        return new ApiException(HttpStatus.BAD_REQUEST, errorCode, message);
    }

    public static ApiException forbidden(String errorCode, String message) {
        return new ApiException(HttpStatus.FORBIDDEN, errorCode, message);
    }

    public HttpStatus getStatus() { return status; }
    public String getErrorCode() { return errorCode; }
}
