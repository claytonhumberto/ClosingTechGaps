using System.ComponentModel.DataAnnotations;

namespace ClosingTechGaps.Application.DTOs;

public record ContactInfoDto(
    [Required, EmailAddress, StringLength(200)] string Email,
    [Required, StringLength(30)] string Phone
);
