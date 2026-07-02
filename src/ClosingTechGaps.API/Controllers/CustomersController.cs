using ClosingTechGaps.API.Filters;
using ClosingTechGaps.Application.DTOs;
using ClosingTechGaps.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace ClosingTechGaps.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CustomersController(ICustomerService service) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var customers = await service.GetAllAsync(ct);
        return Ok(customers);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1)
            return BadRequest("O número da página deve ser maior que zero.");
        if (pageSize < 1 || pageSize > 100)
            return BadRequest("O tamanho da página deve estar entre 1 e 100.");

        var result = await service.GetPagedAsync(page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("queryable")]
    public async Task<IActionResult> GetWithQueryable([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) return BadRequest("Page must be greater than zero.");
        if (pageSize < 1 || pageSize > 100) return BadRequest("Page size must be between 1 and 100.");
        var result = await service.GetPagedWithIQueryableAsync(page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("enumerable")]
    public async Task<IActionResult> GetWithEnumerable([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) return BadRequest("Page must be greater than zero.");
        if (pageSize < 1 || pageSize > 100) return BadRequest("Page size must be between 1 and 100.");
        var result = await service.GetPagedWithIEnumerableAsync(page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("lazy")]
    public async Task<IActionResult> GetWithLazy([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) return BadRequest("Page must be greater than zero.");
        if (pageSize < 1 || pageSize > 100) return BadRequest("Page size must be between 1 and 100.");
        return Ok(await service.GetWithLazyLoadingAsync(page, pageSize, ct));
    }

    [HttpGet("eager")]
    public async Task<IActionResult> GetWithEager([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (page < 1) return BadRequest("Page must be greater than zero.");
        if (pageSize < 1 || pageSize > 100) return BadRequest("Page size must be between 1 and 100.");
        return Ok(await service.GetWithEagerLoadingAsync(page, pageSize, ct));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var customer = await service.GetByIdAsync(id, ct);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPost("idempotent")]
    [ServiceFilter(typeof(IdempotencyFilter))]
    public async Task<IActionResult> CreateIdempotent([FromBody] CreateCustomerDto dto, CancellationToken ct)
    {
        var created = await service.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CreateCustomerDto dto, CancellationToken ct)
    {
        await service.UpdateAsync(id, dto, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return NoContent();
    }
}
