namespace ClosingTechGaps.Web.Models;

public record AddressViewModel(string Street, string City, string State, string ZipCode, string Country);
public record ContactInfoViewModel(string Email, string Phone);

public record CustomerViewModel(
    Guid Id,
    string Name,
    DateOnly BirthdayDate,
    AddressViewModel Address,
    ContactInfoViewModel ContactInfo
);

public record PagedResultViewModel<T>(
    IEnumerable<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasPreviousPage,
    bool HasNextPage
);
