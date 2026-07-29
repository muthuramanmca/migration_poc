package com.example.dummyapi.user;

import com.example.dummyapi.common.ApiException;
import com.example.dummyapi.security.JwtService;
import com.example.dummyapi.user.dto.UserDtos.LoginRequest;
import com.example.dummyapi.user.dto.UserDtos.RegisterRequest;
import com.example.dummyapi.user.dto.UserDtos.UserResponse;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;
import org.springframework.security.crypto.password.PasswordEncoder;

import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.ArgumentMatchers.any;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

class UserServiceTest {

    private UserRepository userRepository;
    private PasswordEncoder passwordEncoder;
    private JwtService jwtService;
    private UserService userService;

    @BeforeEach
    void setUp() {
        userRepository = mock(UserRepository.class);
        passwordEncoder = new BCryptPasswordEncoder();
        jwtService = mock(JwtService.class);
        userService = new UserService(userRepository, passwordEncoder, jwtService);
    }

    @Test
    void register_rejectsDuplicateUsername() {
        when(userRepository.existsByUsername("alice")).thenReturn(true);

        RegisterRequest request = new RegisterRequest("alice", "alice@example.com", "password1");

        assertThatThrownBy(() -> userService.register(request))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Username");
    }

    @Test
    void register_rejectsDuplicateEmail() {
        when(userRepository.existsByUsername("alice")).thenReturn(false);
        when(userRepository.existsByEmail("alice@example.com")).thenReturn(true);

        RegisterRequest request = new RegisterRequest("alice", "alice@example.com", "password1");

        assertThatThrownBy(() -> userService.register(request))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Email");
    }

    @Test
    void register_newUserDefaultsToUserRole() {
        RegisterRequest request = new RegisterRequest("bob", "bob@example.com", "password1");

        UserResponse response = userService.register(request);

        assertThat(response.role()).isEqualTo(Role.USER);
        verify(userRepository).save(any(User.class));
    }

    @Test
    void login_rejectsUnknownUsername() {
        when(userRepository.findByUsername("ghost")).thenReturn(Optional.empty());

        assertThatThrownBy(() -> userService.login(new LoginRequest("ghost", "whatever1")))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Invalid username or password");
    }

    @Test
    void login_rejectsWrongPassword() {
        User existing = new User("carl", "carl@example.com", passwordEncoder.encode("correct1"), Role.USER);
        when(userRepository.findByUsername("carl")).thenReturn(Optional.of(existing));

        assertThatThrownBy(() -> userService.login(new LoginRequest("carl", "wrong1")))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("Invalid username or password");
    }
}
