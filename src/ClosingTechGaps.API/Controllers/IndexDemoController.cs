using ClosingTechGaps.Infrastructure.IndexDemo;
using Microsoft.AspNetCore.Mvc;

namespace ClosingTechGaps.API.Controllers;

[ApiController]
[Route("api/demo/indexes")]
public class IndexDemoController(IndexDemoService svc) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> Status()
    {
        var count = await svc.EnsureSeededAsync();
        return Ok(new { rowCount = count, isSeeded = count > 0 });
    }

    [HttpGet("nonclustered")]
    public async Task<IActionResult> NonClustered([FromQuery] string category = "Electronics")
        => Ok(await svc.NonClusteredAsync(category));

    [HttpGet("unique")]
    public async Task<IActionResult> Unique()
        => Ok(await svc.UniqueAsync());

    [HttpGet("covering")]
    public async Task<IActionResult> Covering([FromQuery] string category = "Electronics")
        => Ok(await svc.CoveringAsync(category));

    [HttpGet("filtered")]
    public async Task<IActionResult> Filtered([FromQuery] string category = "Books")
        => Ok(await svc.FilteredAsync(category));

    [HttpGet("clustered")]
    public async Task<IActionResult> Clustered()
        => Ok(await svc.ClusteredAsync());

    [HttpGet("fulltext")]
    public async Task<IActionResult> FullText([FromQuery] string term = "excellent features")
        => Ok(await svc.FullTextAsync(term));
}
