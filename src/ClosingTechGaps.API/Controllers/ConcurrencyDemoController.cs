using ClosingTechGaps.Infrastructure.ConcurrencyDemo;
using Microsoft.AspNetCore.Mvc;

namespace ClosingTechGaps.API.Controllers;

public record ResetRequest(decimal StartingBalance);
public record DebitRequest(string AccountId, decimal Amount);

[ApiController]
[Route("api/demo/concurrency")]
public class ConcurrencyDemoController(ConcurrencyDemoService demo) : ControllerBase
{
    [HttpPost("reset")]
    public async Task<IActionResult> Reset([FromBody] ResetRequest request)
    {
        var state = await demo.ResetDemoAccountAsync(request.StartingBalance);
        return Ok(state);
    }

    [HttpGet("state/{accountId}")]
    public async Task<IActionResult> GetState(string accountId)
    {
        var state = await demo.GetStateAsync(accountId);
        return Ok(state);
    }

    [HttpPost("debit/naive")]
    public async Task<IActionResult> DebitNaive([FromBody] DebitRequest request)
    {
        var result = await demo.DebitNaiveAsync(request.AccountId, request.Amount);
        return Ok(result);
    }

    [HttpPost("debit/safe")]
    public async Task<IActionResult> DebitSafe([FromBody] DebitRequest request, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
            return BadRequest(new { error = "Header 'Idempotency-Key' is required for this endpoint." });

        var result = await demo.DebitSafeAsync(request.AccountId, request.Amount, idempotencyKey);
        return Ok(result);
    }

    [HttpPost("reverse/{paymentId}")]
    public async Task<IActionResult> Reverse(string paymentId)
    {
        var result = await demo.ReverseAsync(paymentId);
        return Ok(result);
    }
}
