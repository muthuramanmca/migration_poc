#!/usr/bin/env python3
"""
Scaffolds the "dummy-api" Spring Boot project (Auth/Users, Products, Orders)
used as the stand-in Java source application for the Java -> .NET Core
migration plan.

Usage:
    python3 dummy_api_scaffold.py [target-folder]

Default target folder: ./java-api

After running, see the printed instructions (also in the generated README.md)
for how to run the app, view the OpenAPI contract (Phase 2a), and run the tests.
"""

import sys
from pathlib import Path

FILES = {}

FILES["pom.xml"] = """<?xml version="1.0" encoding="UTF-8"?>
<project xmlns="http://maven.apache.org/POM/4.0.0"
         xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
         xsi:schemaLocation="http://maven.apache.org/POM/4.0.0 https://maven.apache.org/xsd/maven-4.0.0.xsd">
    <modelVersion>4.0.0</modelVersion>

    <parent>
        <groupId>org.springframework.boot</groupId>
        <artifactId>spring-boot-starter-parent</artifactId>
        <version>3.2.5</version>
        <relativePath/>
    </parent>

    <groupId>com.example</groupId>
    <artifactId>java-api</artifactId>
    <version>0.1.0</version>
    <name>java-api</name>
    <description>
        Dummy Spring Boot REST API (Auth/Users, Products, Orders) used as a stand-in
        source application for the Java -&gt; .NET Core migration process.
    </description>

    <properties>
        <java.version>17</java.version>
        <jjwt.version>0.12.5</jjwt.version>
        <springdoc.version>2.5.0</springdoc.version>
    </properties>

    <dependencies>
        <dependency>
            <groupId>org.springframework.boot</groupId>
            <artifactId>spring-boot-starter-web</artifactId>
        </dependency>
        <dependency>
            <groupId>org.springframework.boot</groupId>
            <artifactId>spring-boot-starter-data-jpa</artifactId>
        </dependency>
        <dependency>
            <groupId>org.springframework.boot</groupId>
            <artifactId>spring-boot-starter-security</artifactId>
        </dependency>
        <dependency>
            <groupId>org.springframework.boot</groupId>
            <artifactId>spring-boot-starter-validation</artifactId>
        </dependency>

        <dependency>
            <groupId>com.h2database</groupId>
            <artifactId>h2</artifactId>
            <scope>runtime</scope>
        </dependency>

        <!-- OpenAPI / Swagger contract generation -> used in Phase 2a of the migration plan -->
        <dependency>
            <groupId>org.springdoc</groupId>
            <artifactId>springdoc-openapi-starter-webmvc-ui</artifactId>
            <version>${springdoc.version}</version>
        </dependency>

        <!-- JWT issuing/validation -->
        <dependency>
            <groupId>io.jsonwebtoken</groupId>
            <artifactId>jjwt-api</artifactId>
            <version>${jjwt.version}</version>
        </dependency>
        <dependency>
            <groupId>io.jsonwebtoken</groupId>
            <artifactId>jjwt-impl</artifactId>
            <version>${jjwt.version}</version>
            <scope>runtime</scope>
        </dependency>
        <dependency>
            <groupId>io.jsonwebtoken</groupId>
            <artifactId>jjwt-jackson</artifactId>
            <version>${jjwt.version}</version>
            <scope>runtime</scope>
        </dependency>

        <dependency>
            <groupId>org.springframework.boot</groupId>
            <artifactId>spring-boot-starter-test</artifactId>
            <scope>test</scope>
        </dependency>
        <dependency>
            <groupId>org.springframework.security</groupId>
            <artifactId>spring-security-test</artifactId>
            <scope>test</scope>
        </dependency>
    </dependencies>

    <build>
        <plugins>
            <plugin>
                <groupId>org.springframework.boot</groupId>
                <artifactId>spring-boot-maven-plugin</artifactId>
            </plugin>
        </plugins>
    </build>
</project>
"""

FILES[".gitignore"] = """target/
*.class
.idea/
*.iml
.vscode/
"""

FILES["README.md"] = """# dummy-api

A small, deliberately realistic Spring Boot REST API used as the source
application for the **Java -> .NET Core migration exercise**. It is not meant
to be a production app -- it exists so the migration process (OpenAPI
extraction, slice grouping/ordering, the Phase 3 rewrite loop, the Phase 3a
logic-extraction toolkit) has real Java code, real business rules, and a
real running app to practice against.

## Modules (maps directly to the plan's example queue)

| Module      | Package                          | Depends on |
|-------------|-----------------------------------|------------|
| Auth/Users  | `com.example.dummyapi.user`       | -- (foundational) |
| Products    | `com.example.dummyapi.product`    | -- |
| Orders      | `com.example.dummyapi.order`      | Users, Products |

This matches the "Auth/Users -> Products -> Orders" example processing order
used in the Phase 2b flowchart.

## Run it

Requires Java 17+ and Maven.

```bash
mvn spring-boot:run
```

The app starts on `http://localhost:8080` with an in-memory H2 database
(no external setup needed; data resets every restart).

## Phase 2a -- extract the API contract

```bash
curl http://localhost:8080/v3/api-docs
```

or open `http://localhost:8080/swagger-ui.html` in a browser. This is the
machine-readable contract the migration plan says to pull before hand-cataloguing
endpoints.

## Run the tests (Phase 3a -- "mine the existing test suite" in practice)

```bash
mvn test
```

`UserServiceTest`, `ProductServiceTest`, and `OrderServiceTest` encode the
business rules below as executable assertions -- read these before reading
the service implementations, exactly as Phase 3a recommends.

## Try the business flow end to end

```bash
# 1. Register a user (defaults to role USER)
curl -s -X POST localhost:8080/api/auth/register -H "Content-Type: application/json" \\
  -d '{"username":"alice","email":"alice@example.com","password":"password1"}'

# 2. Log in -> get a JWT
TOKEN=$(curl -s -X POST localhost:8080/api/auth/login -H "Content-Type: application/json" \\
  -d '{"username":"alice","password":"password1"}' | python3 -c "import sys,json;print(json.load(sys.stdin)['token'])")

# 3. Browse products (public)
curl -s localhost:8080/api/products

# 4. Placing an order requires a product to exist first -- creating one requires ADMIN.
#    Promote alice to ADMIN via the H2 console (see below), log in again for a fresh
#    token, then:
curl -s -X POST localhost:8080/api/products -H "Content-Type: application/json" \\
  -H "Authorization: Bearer $TOKEN" \\
  -d '{"sku":"SKU-1","name":"Widget","description":"a widget","price":19.99,"stockQuantity":5}'

# 5. Place an order (any authenticated user)
curl -s -X POST localhost:8080/api/orders -H "Content-Type: application/json" \\
  -H "Authorization: Bearer $TOKEN" \\
  -d '{"items":[{"productId":1,"quantity":2}]}'

# 6. Walk the order through its state machine
curl -s -X POST localhost:8080/api/orders/1/pay      -H "Authorization: Bearer $TOKEN"
curl -s -X POST localhost:8080/api/orders/1/ship      -H "Authorization: Bearer $TOKEN"   # ADMIN only
curl -s -X POST localhost:8080/api/orders/1/deliver   -H "Authorization: Bearer $TOKEN"   # ADMIN only
# or, from PENDING/PAID instead of the above:
curl -s -X POST localhost:8080/api/orders/1/cancel    -H "Authorization: Bearer $TOKEN"
```

There is no seed/admin-promotion endpoint on purpose (mirrors a realistic
app). To make a test user ADMIN, use the H2 console at
`http://localhost:8080/h2-console` (JDBC URL `jdbc:h2:mem:dummydb`, user
`sa`, empty password):

```sql
UPDATE USERS SET ROLE = 'ADMIN' WHERE USERNAME = 'alice';
```

Then log in again -- the new JWT will carry the ADMIN role claim.

## Business rules deliberately planted for the Phase 3a exercise

These are the kind of rules the migration plan's Phase 3a toolkit is
designed to surface -- worth deliberately hunting for rather than assuming
a straight read of the Controller/Service layer catches everything:

- **`Product.isLowStock()`** -- logic living in an entity getter, not the
  service layer.
- **`app.products.low-stock-threshold`** in `application.yml` -- a business
  rule driven by configuration, not a hardcoded constant.
- **`Order.transitionTo(...)`** -- the full order status state machine
  (PENDING -> PAID -> SHIPPED -> DELIVERED, with CANCELLED reachable from
  PENDING or PAID only) lives on the entity, not in `OrderService`.
- **Cancelling an order restocks items whether it was PENDING or PAID** --
  because stock is reserved at order-creation time, before any payment
  step exists. Easy to assume restocking only applies to paid orders; it
  doesn't.
- **New registrations always get role `USER`** -- role is never
  client-suppliable at signup (see `UserService.register`).
- **Order ownership check** -- a non-admin can only see/pay/cancel their
  own orders (`OrderService.assertOwnerOrAdmin`), enforced in the service,
  not just via URL structure.
- **`@ValidPassword`** -- a custom Bean Validation annotation
  (min 8 chars + at least one digit), easy to miss if you only skim DTOs
  for `@NotBlank`/`@Size`.
- **Unmapped `AccessDeniedException`** -- there's no explicit
  `@ExceptionHandler` for it; Spring Security's filter chain maps it to
  403 automatically. A rule that isn't in `GlobalExceptionHandler` at all
  is still a rule the .NET rewrite needs to reproduce.
- **`OrderItem.unitPriceAtPurchase`** -- price is captured at order time
  so later `Product` price changes don't retroactively change historical
  order totals.

## Note on this build

This project was generated in a sandbox that could not run a live Maven
build/test cycle to verify compilation end to end. Please run `mvn test`
and `mvn spring-boot:run` locally and report back anything that doesn't
compile or behave as described -- happy to fix it.
"""

FILES["src/main/resources/application.yml"] = """spring:
  application:
    name: dummy-api
  datasource:
    url: jdbc:h2:mem:dummydb;DB_CLOSE_DELAY=-1
    driver-class-name: org.h2.Driver
    username: sa
    password:
  jpa:
    hibernate:
      ddl-auto: update
    show-sql: false
    open-in-view: false
  h2:
    console:
      enabled: true
      path: /h2-console

server:
  port: 8081

springdoc:
  swagger-ui:
    path: /swagger-ui.html
  api-docs:
    path: /v3/api-docs

# Demo only: a real deployment reads secrets like this from a secrets
# manager (see Section 8 of the migration plan), not application.yml.
app:
  jwt:
    secret: "dummy-api-demo-jwt-secret-please-change-in-production-1234567890"
  products:
    low-stock-threshold: 10
"""

FILES["src/main/java/com/example/dummyapi/DummyApiApplication.java"] = """package com.example.dummyapi;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;

@SpringBootApplication
public class DummyApiApplication {
    public static void main(String[] args) {
        SpringApplication.run(DummyApiApplication.class, args);
    }
}
"""

FILES["src/main/java/com/example/dummyapi/config/OpenApiConfig.java"] = """package com.example.dummyapi.config;

import io.swagger.v3.oas.annotations.OpenAPIDefinition;
import io.swagger.v3.oas.annotations.enums.SecuritySchemeType;
import io.swagger.v3.oas.annotations.info.Info;
import io.swagger.v3.oas.annotations.security.SecurityScheme;
import org.springframework.context.annotation.Configuration;

@Configuration
@OpenAPIDefinition(
        info = @Info(
                title = "Dummy API",
                version = "0.1.0",
                description = "Sample Auth/Users, Products and Orders API used as the source " +
                        "application for the Java -> .NET Core migration exercise. " +
                        "Export the contract at /v3/api-docs (Phase 2a)."
        )
)
@SecurityScheme(
        name = "bearerAuth",
        type = SecuritySchemeType.HTTP,
        scheme = "bearer",
        bearerFormat = "JWT"
)
public class OpenApiConfig {
}
"""

FILES["src/main/java/com/example/dummyapi/config/SecurityConfig.java"] = """package com.example.dummyapi.config;

import com.example.dummyapi.security.JwtAuthFilter;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.annotation.web.configurers.AbstractHttpConfigurer;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.crypto.bcrypt.BCryptPasswordEncoder;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.authentication.UsernamePasswordAuthenticationFilter;

@Configuration
public class SecurityConfig {

    private final JwtAuthFilter jwtAuthFilter;

    public SecurityConfig(JwtAuthFilter jwtAuthFilter) {
        this.jwtAuthFilter = jwtAuthFilter;
    }

    @Bean
    public PasswordEncoder passwordEncoder() {
        return new BCryptPasswordEncoder();
    }

    @Bean
    public SecurityFilterChain securityFilterChain(HttpSecurity http) throws Exception {
        http
            .csrf(AbstractHttpConfigurer::disable)
            .sessionManagement(sm -> sm.sessionCreationPolicy(SessionCreationPolicy.STATELESS))
            .authorizeHttpRequests(auth -> auth
                // Public: registration, login, browsing the catalog, and the API contract itself
                .requestMatchers("/api/auth/**").permitAll()
                .requestMatchers("GET", "/api/products/**").permitAll()
                .requestMatchers("/v3/api-docs/**", "/swagger-ui/**", "/swagger-ui.html", "/h2-console/**").permitAll()
                // Admin-only: catalog and fulfilment mutations
                .requestMatchers("POST", "/api/products/**").hasRole("ADMIN")
                .requestMatchers("PUT", "/api/products/**").hasRole("ADMIN")
                .requestMatchers("POST", "/api/orders/*/ship").hasRole("ADMIN")
                .requestMatchers("POST", "/api/orders/*/deliver").hasRole("ADMIN")
                // Everything else requires a logged-in user
                .anyRequest().authenticated()
            )
            .headers(headers -> headers.frameOptions(frame -> frame.disable())) // needed for /h2-console
            .addFilterBefore(jwtAuthFilter, UsernamePasswordAuthenticationFilter.class);

        return http.build();
    }
}
"""

FILES["src/main/java/com/example/dummyapi/security/JwtService.java"] = """package com.example.dummyapi.security;

import io.jsonwebtoken.Claims;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.security.Keys;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;

import javax.crypto.SecretKey;
import java.util.Date;

@Service
public class JwtService {

    // Demo-only: a real deployment reads this from a secrets manager, not application.yml.
    public static final long EXPIRY_SECONDS = 3600;

    private final SecretKey key;

    public JwtService(@Value("${app.jwt.secret}") String secret) {
        this.key = Keys.hmacShaKeyFor(secret.getBytes());
    }

    public String generateToken(String username, String role) {
        Date now = new Date();
        Date expiry = new Date(now.getTime() + EXPIRY_SECONDS * 1000);
        return Jwts.builder()
                .subject(username)
                .claim("role", role)
                .issuedAt(now)
                .expiration(expiry)
                .signWith(key)
                .compact();
    }

    public Claims parse(String token) {
        return Jwts.parser()
                .verifyWith(key)
                .build()
                .parseSignedClaims(token)
                .getPayload();
    }
}
"""

FILES["src/main/java/com/example/dummyapi/security/JwtAuthFilter.java"] = """package com.example.dummyapi.security;

import io.jsonwebtoken.Claims;
import io.jsonwebtoken.JwtException;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.springframework.security.authentication.UsernamePasswordAuthenticationToken;
import org.springframework.security.core.authority.SimpleGrantedAuthority;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;

import java.io.IOException;
import java.util.List;

@Component
public class JwtAuthFilter extends OncePerRequestFilter {

    private final JwtService jwtService;

    public JwtAuthFilter(JwtService jwtService) {
        this.jwtService = jwtService;
    }

    @Override
    protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response, FilterChain chain)
            throws ServletException, IOException {

        String header = request.getHeader("Authorization");
        if (header != null && header.startsWith("Bearer ")) {
            String token = header.substring(7);
            try {
                Claims claims = jwtService.parse(token);
                String username = claims.getSubject();
                String role = claims.get("role", String.class);

                var authorities = List.of(new SimpleGrantedAuthority("ROLE_" + role));
                var authentication = new UsernamePasswordAuthenticationToken(username, null, authorities);
                SecurityContextHolder.getContext().setAuthentication(authentication);
            } catch (JwtException | IllegalArgumentException ex) {
                // Invalid/expired token: leave the request unauthenticated. Downstream
                // authorization rules (see SecurityConfig) will reject it with 401/403.
                SecurityContextHolder.clearContext();
            }
        }
        chain.doFilter(request, response);
    }
}
"""

FILES["src/main/java/com/example/dummyapi/common/ApiException.java"] = """package com.example.dummyapi.common;

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
"""

FILES["src/main/java/com/example/dummyapi/common/ApiError.java"] = """package com.example.dummyapi.common;

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
"""

FILES["src/main/java/com/example/dummyapi/common/GlobalExceptionHandler.java"] = """package com.example.dummyapi.common;

import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;

import java.util.List;

@RestControllerAdvice
public class GlobalExceptionHandler {

    @ExceptionHandler(ApiException.class)
    public ResponseEntity<ApiError> handleApiException(ApiException ex) {
        ApiError body = ApiError.of(ex.getStatus().value(), ex.getErrorCode(), ex.getMessage());
        return ResponseEntity.status(ex.getStatus()).body(body);
    }

    @ExceptionHandler(MethodArgumentNotValidException.class)
    public ResponseEntity<ApiError> handleValidation(MethodArgumentNotValidException ex) {
        List<String> fieldErrors = ex.getBindingResult().getFieldErrors().stream()
                .map(fe -> fe.getField() + ": " + fe.getDefaultMessage())
                .toList();
        ApiError body = ApiError.of(400, "VALIDATION_FAILED", "Request validation failed", fieldErrors);
        return ResponseEntity.badRequest().body(body);
    }
}
"""

FILES["src/main/java/com/example/dummyapi/common/ValidPassword.java"] = """package com.example.dummyapi.common;

import jakarta.validation.Constraint;
import jakarta.validation.Payload;

import java.lang.annotation.Documented;
import java.lang.annotation.ElementType;
import java.lang.annotation.Retention;
import java.lang.annotation.RetentionPolicy;
import java.lang.annotation.Target;

@Documented
@Constraint(validatedBy = PasswordValidator.class)
@Target({ElementType.FIELD, ElementType.PARAMETER})
@Retention(RetentionPolicy.RUNTIME)
public @interface ValidPassword {
    String message() default "Password must be at least 8 characters and contain at least one digit";
    Class<?>[] groups() default {};
    Class<? extends Payload>[] payload() default {};
}
"""

FILES["src/main/java/com/example/dummyapi/common/PasswordValidator.java"] = """package com.example.dummyapi.common;

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
"""

FILES["src/main/java/com/example/dummyapi/user/Role.java"] = """package com.example.dummyapi.user;

public enum Role {
    USER,
    ADMIN
}
"""

FILES["src/main/java/com/example/dummyapi/user/User.java"] = """package com.example.dummyapi.user;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.UniqueConstraint;

import java.time.Instant;

@Entity
@Table(name = "users", uniqueConstraints = {
        @UniqueConstraint(columnNames = "username"),
        @UniqueConstraint(columnNames = "email")
})
public class User {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private String username;

    @Column(nullable = false)
    private String email;

    @Column(nullable = false)
    private String passwordHash;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private Role role;

    @Column(nullable = false)
    private Instant createdAt = Instant.now();

    protected User() {
        // JPA
    }

    public User(String username, String email, String passwordHash, Role role) {
        this.username = username;
        this.email = email;
        this.passwordHash = passwordHash;
        this.role = role;
    }

    public Long getId() { return id; }
    public String getUsername() { return username; }
    public String getEmail() { return email; }
    public String getPasswordHash() { return passwordHash; }
    public Role getRole() { return role; }
    public Instant getCreatedAt() { return createdAt; }
}
"""

FILES["src/main/java/com/example/dummyapi/user/UserRepository.java"] = """package com.example.dummyapi.user;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.Optional;

public interface UserRepository extends JpaRepository<User, Long> {
    Optional<User> findByUsername(String username);
    boolean existsByUsername(String username);
    boolean existsByEmail(String email);
}
"""

FILES["src/main/java/com/example/dummyapi/user/dto/UserDtos.java"] = """package com.example.dummyapi.user.dto;

import com.example.dummyapi.common.ValidPassword;
import com.example.dummyapi.user.Role;
import jakarta.validation.constraints.Email;
import jakarta.validation.constraints.NotBlank;
import jakarta.validation.constraints.Size;

public class UserDtos {

    public record RegisterRequest(
            @NotBlank @Size(min = 3, max = 30) String username,
            @NotBlank @Email String email,
            @ValidPassword String password
    ) {}

    public record LoginRequest(
            @NotBlank String username,
            @NotBlank String password
    ) {}

    public record AuthResponse(String token, long expiresInSeconds) {}

    public record UserResponse(Long id, String username, String email, Role role) {}
}
"""

FILES["src/main/java/com/example/dummyapi/user/UserService.java"] = """package com.example.dummyapi.user;

import com.example.dummyapi.common.ApiException;
import com.example.dummyapi.security.JwtService;
import com.example.dummyapi.user.dto.UserDtos.AuthResponse;
import com.example.dummyapi.user.dto.UserDtos.LoginRequest;
import com.example.dummyapi.user.dto.UserDtos.RegisterRequest;
import com.example.dummyapi.user.dto.UserDtos.UserResponse;
import org.springframework.security.crypto.password.PasswordEncoder;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

@Service
public class UserService {

    private final UserRepository userRepository;
    private final PasswordEncoder passwordEncoder;
    private final JwtService jwtService;

    public UserService(UserRepository userRepository, PasswordEncoder passwordEncoder, JwtService jwtService) {
        this.userRepository = userRepository;
        this.passwordEncoder = passwordEncoder;
        this.jwtService = jwtService;
    }

    @Transactional
    public UserResponse register(RegisterRequest request) {
        if (userRepository.existsByUsername(request.username())) {
            throw ApiException.conflict("DUPLICATE_USERNAME", "Username is already taken");
        }
        if (userRepository.existsByEmail(request.email())) {
            throw ApiException.conflict("DUPLICATE_EMAIL", "Email is already registered");
        }

        // New registrations always start as USER; only an existing ADMIN could
        // promote someone later (not exposed in this dummy app). The rule that
        // "role is never client-supplied at signup" matters for the .NET rewrite.
        User user = new User(
                request.username(),
                request.email(),
                passwordEncoder.encode(request.password()),
                Role.USER
        );
        userRepository.save(user);
        return toResponse(user);
    }

    public AuthResponse login(LoginRequest request) {
        User user = userRepository.findByUsername(request.username())
                .orElseThrow(() -> ApiException.unauthorized("INVALID_CREDENTIALS", "Invalid username or password"));

        if (!passwordEncoder.matches(request.password(), user.getPasswordHash())) {
            throw ApiException.unauthorized("INVALID_CREDENTIALS", "Invalid username or password");
        }

        String token = jwtService.generateToken(user.getUsername(), user.getRole().name());
        return new AuthResponse(token, JwtService.EXPIRY_SECONDS);
    }

    public UserResponse getByUsername(String username) {
        User user = userRepository.findByUsername(username)
                .orElseThrow(() -> ApiException.notFound("USER_NOT_FOUND", "User not found: " + username));
        return toResponse(user);
    }

    private UserResponse toResponse(User user) {
        return new UserResponse(user.getId(), user.getUsername(), user.getEmail(), user.getRole());
    }
}
"""

FILES["src/main/java/com/example/dummyapi/user/AuthController.java"] = """package com.example.dummyapi.user;

import com.example.dummyapi.user.dto.UserDtos.AuthResponse;
import com.example.dummyapi.user.dto.UserDtos.LoginRequest;
import com.example.dummyapi.user.dto.UserDtos.RegisterRequest;
import com.example.dummyapi.user.dto.UserDtos.UserResponse;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/auth")
public class AuthController {

    private final UserService userService;

    public AuthController(UserService userService) {
        this.userService = userService;
    }

    @PostMapping("/register")
    public ResponseEntity<UserResponse> register(@Valid @RequestBody RegisterRequest request) {
        return ResponseEntity.status(201).body(userService.register(request));
    }

    @PostMapping("/login")
    public AuthResponse login(@Valid @RequestBody LoginRequest request) {
        return userService.login(request);
    }
}
"""

FILES["src/main/java/com/example/dummyapi/user/UserController.java"] = """package com.example.dummyapi.user;

import com.example.dummyapi.user.dto.UserDtos.UserResponse;
import org.springframework.security.core.Authentication;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/users")
public class UserController {

    private final UserService userService;

    public UserController(UserService userService) {
        this.userService = userService;
    }

    @GetMapping("/me")
    public UserResponse me(Authentication authentication) {
        return userService.getByUsername(authentication.getName());
    }
}
"""

FILES["src/main/java/com/example/dummyapi/product/Product.java"] = """package com.example.dummyapi.product;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.Table;
import jakarta.persistence.Transient;
import jakarta.persistence.UniqueConstraint;

import java.math.BigDecimal;

@Entity
@Table(name = "products", uniqueConstraints = @UniqueConstraint(columnNames = "sku"))
public class Product {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private String sku;

    @Column(nullable = false)
    private String name;

    private String description;

    @Column(nullable = false)
    private BigDecimal price;

    @Column(nullable = false)
    private int stockQuantity;

    // Populated at read-time by ProductService from application.yml
    // (app.products.low-stock-threshold) -- not persisted.
    @Transient
    private int lowStockThreshold = 10;

    protected Product() {
        // JPA
    }

    public Product(String sku, String name, String description, BigDecimal price, int stockQuantity) {
        this.sku = sku;
        this.name = name;
        this.description = description;
        this.price = price;
        this.stockQuantity = stockQuantity;
    }

    /**
     * Business logic living in an entity getter rather than the service layer -
     * intentionally included: this is exactly the kind of rule that's easy to
     * miss when a migration only reads Service classes.
     */
    public boolean isLowStock() {
        return stockQuantity < lowStockThreshold;
    }

    public void setLowStockThreshold(int lowStockThreshold) {
        this.lowStockThreshold = lowStockThreshold;
    }

    public void decreaseStock(int quantity) {
        this.stockQuantity -= quantity;
    }

    public void increaseStock(int quantity) {
        this.stockQuantity += quantity;
    }

    public Long getId() { return id; }
    public String getSku() { return sku; }
    public String getName() { return name; }
    public String getDescription() { return description; }
    public BigDecimal getPrice() { return price; }
    public int getStockQuantity() { return stockQuantity; }
}
"""

FILES["src/main/java/com/example/dummyapi/product/ProductRepository.java"] = """package com.example.dummyapi.product;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.Optional;

public interface ProductRepository extends JpaRepository<Product, Long> {
    boolean existsBySku(String sku);
    Optional<Product> findBySku(String sku);
}
"""

FILES["src/main/java/com/example/dummyapi/product/dto/ProductDtos.java"] = """package com.example.dummyapi.product.dto;

import jakarta.validation.constraints.DecimalMin;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotBlank;

import java.math.BigDecimal;

public class ProductDtos {

    public record ProductRequest(
            @NotBlank String sku,
            @NotBlank String name,
            String description,
            @DecimalMin(value = "0.01", message = "Price must be greater than zero") BigDecimal price,
            @Min(value = 0, message = "Initial stock cannot be negative") int stockQuantity
    ) {}

    public record StockAdjustmentRequest(int delta) {}

    public record ProductResponse(
            Long id,
            String sku,
            String name,
            String description,
            BigDecimal price,
            int stockQuantity,
            boolean lowStock
    ) {}
}
"""

FILES["src/main/java/com/example/dummyapi/product/ProductService.java"] = """package com.example.dummyapi.product;

import com.example.dummyapi.common.ApiException;
import com.example.dummyapi.product.dto.ProductDtos.ProductRequest;
import com.example.dummyapi.product.dto.ProductDtos.ProductResponse;
import com.example.dummyapi.product.dto.ProductDtos.StockAdjustmentRequest;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.util.List;

@Service
public class ProductService {

    private final ProductRepository productRepository;

    // Config-driven business rule: the low-stock threshold is not hardcoded,
    // it comes from application.yml (app.products.low-stock-threshold).
    @Value("${app.products.low-stock-threshold}")
    private int lowStockThreshold;

    public ProductService(ProductRepository productRepository) {
        this.productRepository = productRepository;
    }

    @Transactional
    public ProductResponse create(ProductRequest request) {
        if (productRepository.existsBySku(request.sku())) {
            throw ApiException.conflict("DUPLICATE_SKU", "A product with this SKU already exists");
        }
        Product product = new Product(
                request.sku(), request.name(), request.description(),
                request.price(), request.stockQuantity()
        );
        product.setLowStockThreshold(lowStockThreshold);
        productRepository.save(product);
        return toResponse(product);
    }

    public ProductResponse getById(Long id) {
        return toResponse(findOrThrow(id));
    }

    public List<ProductResponse> listAll() {
        return productRepository.findAll().stream().map(this::toResponse).toList();
    }

    @Transactional
    public ProductResponse adjustStock(Long id, StockAdjustmentRequest request) {
        Product product = findOrThrow(id);
        int resultingStock = product.getStockQuantity() + request.delta();
        if (resultingStock < 0) {
            throw ApiException.conflict("INSUFFICIENT_STOCK",
                    "Stock adjustment would result in a negative quantity for product " + product.getSku());
        }
        if (request.delta() >= 0) {
            product.increaseStock(request.delta());
        } else {
            product.decreaseStock(-request.delta());
        }
        return toResponse(product);
    }

    /** Public: used by OrderService (a different package) to reserve/restock within a transaction. */
    public Product findOrThrow(Long id) {
        Product product = productRepository.findById(id)
                .orElseThrow(() -> ApiException.notFound("PRODUCT_NOT_FOUND", "Product not found: " + id));
        product.setLowStockThreshold(lowStockThreshold);
        return product;
    }

    private ProductResponse toResponse(Product product) {
        return new ProductResponse(
                product.getId(), product.getSku(), product.getName(), product.getDescription(),
                product.getPrice(), product.getStockQuantity(), product.isLowStock()
        );
    }
}
"""

FILES["src/main/java/com/example/dummyapi/product/ProductController.java"] = """package com.example.dummyapi.product;

import com.example.dummyapi.product.dto.ProductDtos.ProductRequest;
import com.example.dummyapi.product.dto.ProductDtos.ProductResponse;
import com.example.dummyapi.product.dto.ProductDtos.StockAdjustmentRequest;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.PutMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@RestController
@RequestMapping("/api/products")
public class ProductController {

    private final ProductService productService;

    public ProductController(ProductService productService) {
        this.productService = productService;
    }

    @GetMapping
    public List<ProductResponse> list() {
        return productService.listAll();
    }

    @GetMapping("/{id}")
    public ProductResponse get(@PathVariable Long id) {
        return productService.getById(id);
    }

    @PostMapping
    public ResponseEntity<ProductResponse> create(@Valid @RequestBody ProductRequest request) {
        return ResponseEntity.status(201).body(productService.create(request));
    }

    @PutMapping("/{id}/stock")
    public ProductResponse adjustStock(@PathVariable Long id, @RequestBody StockAdjustmentRequest request) {
        return productService.adjustStock(id, request);
    }
}
"""

FILES["src/main/java/com/example/dummyapi/order/OrderStatus.java"] = """package com.example.dummyapi.order;

public enum OrderStatus {
    PENDING,
    PAID,
    SHIPPED,
    DELIVERED,
    CANCELLED
}
"""

FILES["src/main/java/com/example/dummyapi/order/OrderItem.java"] = """package com.example.dummyapi.order;

import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.JoinColumn;
import jakarta.persistence.ManyToOne;
import jakarta.persistence.Table;

import java.math.BigDecimal;

@Entity
@Table(name = "order_items")
public class OrderItem {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "order_id")
    private Order order;

    @Column(nullable = false)
    private Long productId;

    @Column(nullable = false)
    private int quantity;

    // Price is captured at purchase time so later price changes on the
    // Product don't retroactively change historical order totals.
    @Column(nullable = false)
    private BigDecimal unitPriceAtPurchase;

    protected OrderItem() {
        // JPA
    }

    public OrderItem(Long productId, int quantity, BigDecimal unitPriceAtPurchase) {
        this.productId = productId;
        this.quantity = quantity;
        this.unitPriceAtPurchase = unitPriceAtPurchase;
    }

    public BigDecimal lineTotal() {
        return unitPriceAtPurchase.multiply(BigDecimal.valueOf(quantity));
    }

    void setOrder(Order order) { this.order = order; }

    public Long getId() { return id; }
    public Long getProductId() { return productId; }
    public int getQuantity() { return quantity; }
    public BigDecimal getUnitPriceAtPurchase() { return unitPriceAtPurchase; }
}
"""

FILES["src/main/java/com/example/dummyapi/order/Order.java"] = """package com.example.dummyapi.order;

import com.example.dummyapi.common.ApiException;
import jakarta.persistence.CascadeType;
import jakarta.persistence.Column;
import jakarta.persistence.Entity;
import jakarta.persistence.EnumType;
import jakarta.persistence.Enumerated;
import jakarta.persistence.FetchType;
import jakarta.persistence.GeneratedValue;
import jakarta.persistence.GenerationType;
import jakarta.persistence.Id;
import jakarta.persistence.OneToMany;
import jakarta.persistence.Table;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.ArrayList;
import java.util.List;

@Entity
@Table(name = "orders")
public class Order {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Long id;

    @Column(nullable = false)
    private String username;

    @Enumerated(EnumType.STRING)
    @Column(nullable = false)
    private OrderStatus status = OrderStatus.PENDING;

    @Column(nullable = false)
    private BigDecimal totalAmount = BigDecimal.ZERO;

    @Column(nullable = false)
    private Instant createdAt = Instant.now();

    @OneToMany(mappedBy = "order", cascade = CascadeType.ALL, orphanRemoval = true, fetch = FetchType.EAGER)
    private List<OrderItem> items = new ArrayList<>();

    protected Order() {
        // JPA
    }

    public Order(String username) {
        this.username = username;
    }

    public void addItem(OrderItem item) {
        item.setOrder(this);
        items.add(item);
        recalculateTotal();
    }

    private void recalculateTotal() {
        this.totalAmount = items.stream()
                .map(OrderItem::lineTotal)
                .reduce(BigDecimal.ZERO, BigDecimal::add);
    }

    /**
     * The order status state machine. This is the single place the valid
     * transitions are enforced - deliberately placed on the entity (not the
     * service) as another example of business logic that lives outside the
     * "obvious" Service class.
     *
     * PENDING -> PAID -> SHIPPED -> DELIVERED
     * PENDING -> CANCELLED
     * PAID    -> CANCELLED (restocks items - see OrderService.cancel)
     */
    public void transitionTo(OrderStatus target) {
        boolean valid = switch (status) {
            case PENDING -> target == OrderStatus.PAID || target == OrderStatus.CANCELLED;
            case PAID -> target == OrderStatus.SHIPPED || target == OrderStatus.CANCELLED;
            case SHIPPED -> target == OrderStatus.DELIVERED;
            case DELIVERED, CANCELLED -> false;
        };
        if (!valid) {
            throw ApiException.conflict("INVALID_ORDER_STATE",
                    "Cannot transition order from " + status + " to " + target);
        }
        this.status = target;
    }

    public Long getId() { return id; }
    public String getUsername() { return username; }
    public OrderStatus getStatus() { return status; }
    public BigDecimal getTotalAmount() { return totalAmount; }
    public Instant getCreatedAt() { return createdAt; }
    public List<OrderItem> getItems() { return items; }
}
"""

FILES["src/main/java/com/example/dummyapi/order/OrderRepository.java"] = """package com.example.dummyapi.order;

import org.springframework.data.jpa.repository.JpaRepository;

import java.util.List;

public interface OrderRepository extends JpaRepository<Order, Long> {
    List<Order> findByUsername(String username);
}
"""

FILES["src/main/java/com/example/dummyapi/order/dto/OrderDtos.java"] = """package com.example.dummyapi.order.dto;

import jakarta.validation.Valid;
import jakarta.validation.constraints.Min;
import jakarta.validation.constraints.NotEmpty;
import jakarta.validation.constraints.NotNull;

import java.math.BigDecimal;
import java.time.Instant;
import java.util.List;

public class OrderDtos {

    public record OrderLineRequest(
            @NotNull Long productId,
            @Min(1) int quantity
    ) {}

    public record OrderRequest(
            @NotEmpty @Valid List<OrderLineRequest> items
    ) {}

    public record OrderItemResponse(Long productId, int quantity, BigDecimal unitPriceAtPurchase, BigDecimal lineTotal) {}

    public record OrderResponse(
            Long id,
            String username,
            String status,
            BigDecimal totalAmount,
            Instant createdAt,
            List<OrderItemResponse> items
    ) {}
}
"""

FILES["src/main/java/com/example/dummyapi/order/event/OrderCreatedEvent.java"] = """package com.example.dummyapi.order.event;

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
"""

FILES["src/main/java/com/example/dummyapi/order/event/OrderEventListener.java"] = """package com.example.dummyapi.order.event;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.context.event.EventListener;
import org.springframework.stereotype.Component;

@Component
public class OrderEventListener {

    private static final Logger log = LoggerFactory.getLogger(OrderEventListener.class);

    @EventListener
    public void onOrderCreated(OrderCreatedEvent event) {
        log.info("Order {} created for user {} -- would notify fulfilment here",
                event.getOrderId(), event.getUsername());
    }
}
"""

FILES["src/main/java/com/example/dummyapi/order/OrderService.java"] = """package com.example.dummyapi.order;

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
"""

FILES["src/main/java/com/example/dummyapi/order/OrderController.java"] = """package com.example.dummyapi.order;

import com.example.dummyapi.order.dto.OrderDtos.OrderRequest;
import com.example.dummyapi.order.dto.OrderDtos.OrderResponse;
import jakarta.validation.Valid;
import org.springframework.http.ResponseEntity;
import org.springframework.security.core.Authentication;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.PathVariable;
import org.springframework.web.bind.annotation.PostMapping;
import org.springframework.web.bind.annotation.RequestBody;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

import java.util.List;

@RestController
@RequestMapping("/api/orders")
public class OrderController {

    private final OrderService orderService;

    public OrderController(OrderService orderService) {
        this.orderService = orderService;
    }

    @PostMapping
    public ResponseEntity<OrderResponse> create(Authentication auth, @Valid @RequestBody OrderRequest request) {
        return ResponseEntity.status(201).body(orderService.create(auth.getName(), request));
    }

    @GetMapping
    public List<OrderResponse> list(Authentication auth) {
        return orderService.listForUser(auth.getName(), isAdmin(auth));
    }

    @GetMapping("/{id}")
    public OrderResponse get(Authentication auth, @PathVariable Long id) {
        return orderService.getById(auth.getName(), isAdmin(auth), id);
    }

    @PostMapping("/{id}/pay")
    public OrderResponse pay(Authentication auth, @PathVariable Long id) {
        return orderService.pay(auth.getName(), isAdmin(auth), id);
    }

    @PostMapping("/{id}/ship")
    public OrderResponse ship(@PathVariable Long id) {
        return orderService.ship(id);
    }

    @PostMapping("/{id}/deliver")
    public OrderResponse deliver(@PathVariable Long id) {
        return orderService.deliver(id);
    }

    @PostMapping("/{id}/cancel")
    public OrderResponse cancel(Authentication auth, @PathVariable Long id) {
        return orderService.cancel(auth.getName(), isAdmin(auth), id);
    }

    private boolean isAdmin(Authentication auth) {
        return auth.getAuthorities().stream().anyMatch(a -> a.getAuthority().equals("ROLE_ADMIN"));
    }
}
"""

FILES["src/test/java/com/example/dummyapi/user/UserServiceTest.java"] = """package com.example.dummyapi.user;

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
"""

FILES["src/test/java/com/example/dummyapi/product/ProductServiceTest.java"] = """package com.example.dummyapi.product;

import com.example.dummyapi.common.ApiException;
import com.example.dummyapi.product.dto.ProductDtos.ProductRequest;
import com.example.dummyapi.product.dto.ProductDtos.ProductResponse;
import com.example.dummyapi.product.dto.ProductDtos.StockAdjustmentRequest;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;
import org.springframework.test.util.ReflectionTestUtils;

import java.math.BigDecimal;
import java.util.Optional;

import static org.assertj.core.api.Assertions.assertThat;
import static org.assertj.core.api.Assertions.assertThatThrownBy;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.when;

class ProductServiceTest {

    private ProductRepository productRepository;
    private ProductService productService;

    @BeforeEach
    void setUp() {
        productRepository = mock(ProductRepository.class);
        productService = new ProductService(productRepository);
        ReflectionTestUtils.setField(productService, "lowStockThreshold", 10);
    }

    @Test
    void create_rejectsDuplicateSku() {
        when(productRepository.existsBySku("SKU-1")).thenReturn(true);

        ProductRequest request = new ProductRequest("SKU-1", "Widget", "desc", BigDecimal.TEN, 5);

        assertThatThrownBy(() -> productService.create(request))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("SKU");
    }

    @Test
    void create_flagsLowStockWhenBelowThreshold() {
        ProductRequest request = new ProductRequest("SKU-2", "Gadget", "desc", BigDecimal.TEN, 3);

        ProductResponse response = productService.create(request);

        assertThat(response.lowStock()).isTrue();
        assertThat(response.stockQuantity()).isEqualTo(3);
    }

    @Test
    void create_doesNotFlagLowStockAboveThreshold() {
        ProductRequest request = new ProductRequest("SKU-3", "Gizmo", "desc", BigDecimal.TEN, 50);

        ProductResponse response = productService.create(request);

        assertThat(response.lowStock()).isFalse();
    }

    @Test
    void adjustStock_rejectsNegativeResultingQuantity() {
        Product product = new Product("SKU-4", "Doohickey", "desc", BigDecimal.TEN, 5);
        when(productRepository.findById(1L)).thenReturn(Optional.of(product));

        assertThatThrownBy(() -> productService.adjustStock(1L, new StockAdjustmentRequest(-10)))
                .isInstanceOf(ApiException.class)
                .hasMessageContaining("negative");
    }
}
"""

FILES["src/test/java/com/example/dummyapi/order/OrderServiceTest.java"] = """package com.example.dummyapi.order;

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
"""


def main():
    target = Path(sys.argv[1]) if len(sys.argv) > 1 else Path("java-api")
    target.mkdir(parents=True, exist_ok=True)

    for rel_path, content in FILES.items():
        full_path = target / rel_path
        full_path.parent.mkdir(parents=True, exist_ok=True)
        full_path.write_text(content, encoding="utf-8")
        print(f"wrote {full_path}")

    print()
    print(f"Done. {len(FILES)} files written under {target.resolve()}")
    print()
    print("Next steps:")
    print(f"  cd {target}")
    print("  mvn spring-boot:run")
    print("  # in another terminal:")
    print("  curl http://localhost:8080/v3/api-docs      # Phase 2a contract export")
    print("  # or open http://localhost:8080/swagger-ui.html")
    print("  mvn test                                    # Phase 3a test-mining exercise")


if __name__ == "__main__":
    main()
