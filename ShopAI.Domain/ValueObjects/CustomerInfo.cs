namespace Domain.ValueObjects;

public record CustomerInfo(Guid Id, string Name, string Email, string Phone, string Role);
