using System.ComponentModel.DataAnnotations;

namespace ClosingTechGaps.Application.DTOs;

public record AddressDto(
    [Required, StringLength(300)] string Street,
    [Required, StringLength(100)] string City,
    [Required, StringLength(100)] string State,
    [Required, RegularExpression(@"^\d{5}-\d{3}$", ErrorMessage = "CEP inválido. Formato esperado: 00000-000")] string ZipCode,
    [Required, StringLength(100)] string Country
);
