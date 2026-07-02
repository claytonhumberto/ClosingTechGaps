using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClosingTechGaps.Web.Filters;

public class ApiBaseUrlFilter(string apiBaseUrl) : IResultFilter
{
    public void OnResultExecuting(ResultExecutingContext context)
    {
        if (context.Controller is PageModel page)
            page.ViewData["ApiBaseUrl"] = apiBaseUrl;
    }

    public void OnResultExecuted(ResultExecutedContext context) { }
}
