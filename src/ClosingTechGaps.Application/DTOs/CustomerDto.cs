namespace ClosingTechGaps.Application.DTOs;

public record CustomerDto(
    Guid Id,
    string Name,
    DateOnly BirthdayDate,
    AddressDto Address,
    ContactInfoDto ContactInfo
);
