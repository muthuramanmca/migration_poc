package com.example.airlineapi.common;

import jakarta.validation.ConstraintValidator;
import jakarta.validation.ConstraintValidatorContext;

/**
 * Business rule embedded in a validator rather than the service layer -
 * an intentional example of the kind of rule that's easy to miss when
 * cataloguing a Java app's logic by reading only the Service classes.
 */
public class PasswordValidator implements ConstraintValidator<ValidPassword, String> {
    @Override
    public boolean isValid(String value, ConstraintValidatorContext context) {
        if (value == null || value.length() < 8) {
            return false;
        }
        return value.chars().anyMatch(Character::isDigit);
    }
}
