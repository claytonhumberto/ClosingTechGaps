using System.Text.Json;
using ClosingTechGaps.Infrastructure.Idempotency;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ClosingTechGaps.API.Filters;

public class IdempotencyFilter(IIdempotencyStore store) : IAsyncActionFilter
{
    public const string HeaderName = "Idempotency-Key";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var keyValues) ||
            string.IsNullOrWhiteSpace(keyValues))
        {
            context.Result = new BadRequestObjectResult(
                new { error = $"Header '{HeaderName}' is required for this endpoint." });
            return;
        }

        var key = keyValues.ToString().Trim();
        var sem = store.GetLock(key);

        await sem.WaitAsync();
        try
        {
            // Check again inside the lock — another concurrent request may have already stored it
            if (store.TryGet(key, out var cached) && cached is not null)
            {
                context.HttpContext.Response.Headers["X-Idempotency-Replayed"] = "true";
                context.HttpContext.Response.Headers["X-Idempotency-Key"] = key;
                context.HttpContext.Response.Headers["X-Idempotency-Created-At"] = cached.CreatedAt.ToString("o");
                context.Result = new ContentResult
                {
                    StatusCode = cached.StatusCode,
                    ContentType = cached.ContentType,
                    Content = cached.Body
                };
                return;
            }

            var executed = await next();

            if (executed.Result is ObjectResult { Value: not null } objectResult)
            {
                var body = JsonSerializer.Serialize(objectResult.Value,
                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

                store.Set(key, new IdempotencyEntry(
                    StatusCode: objectResult.StatusCode ?? 200,
                    ContentType: "application/json",
                    Body: body,
                    CreatedAt: DateTimeOffset.UtcNow
                ));

                context.HttpContext.Response.Headers["X-Idempotency-Key"] = key;
            }
        }
        finally
        {
            sem.Release();
        }
    }
}
