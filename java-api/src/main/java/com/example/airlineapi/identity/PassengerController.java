package com.example.airlineapi.identity;

import com.example.airlineapi.identity.dto.IdentityDtos.PassengerResponse;
import org.springframework.security.core.Authentication;
import org.springframework.web.bind.annotation.GetMapping;
import org.springframework.web.bind.annotation.RequestMapping;
import org.springframework.web.bind.annotation.RestController;

@RestController
@RequestMapping("/api/passengers")
public class PassengerController {

    private final IdentityService identityService;

    public PassengerController(IdentityService identityService) {
        this.identityService = identityService;
    }

    @GetMapping("/me")
    public PassengerResponse me(Authentication authentication) {
        return identityService.getByUsername(authentication.getName());
    }
}
