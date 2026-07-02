using System.ComponentModel.DataAnnotations;

namespace ClosingTechGaps.Application.DTOs;

public record CreateCustomerDto(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    DateOnly BirthdayDate,
    [Required] AddressDto Address,
    [Required] ContactInfoDto ContactInfo
);
