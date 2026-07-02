using ClosingTechGaps.Infrastructure.SqlInjectionDemo;
using Microsoft.AspNetCore.Mvc;

namespace ClosingTechGaps.API.Controllers;

[ApiController]
[Route("api/demo/sql-injection")]
public class SqlInjectionDemoController(SqlInjectionDemoService demo) : ControllerBase
{
    [HttpGet("unsafe")]
    public IActionResult SearchUnsafe([FromQuery] string name = "")
    {
        var result = demo.SearchUnsafe(name);
        return Ok(result);
    }

    [HttpGet("safe")]
    public IActionResult SearchSafe([FromQuery] string name = "")
    {
        var result = demo.SearchSafe(name);
        return Ok(result);
    }
}
