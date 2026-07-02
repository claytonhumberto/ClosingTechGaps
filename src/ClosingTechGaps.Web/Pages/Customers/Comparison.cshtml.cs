using System.Net.Http.Json;
using ClosingTechGaps.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClosingTechGaps.Web.Pages.Customers;

public class ComparisonModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public PagedWithMetricsViewModel<CustomerViewModel> QueryableResult { get; private set; } = default!;
    public PagedWithMetricsViewModel<CustomerViewModel> EnumerableResult { get; private set; } = default!;

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 20;

    public async Task<IActionResult> OnGetAsync()
    {
        if (CurrentPage < 1) return RedirectToPage(new { currentPage = 1, pageSize = PageSize });
        if (PageSize < 1 || PageSize > 100) return RedirectToPage(new { currentPage = CurrentPage, pageSize = 20 });

        var client = httpClientFactory.CreateClient("CustomerApi");
        var url = $"/api/customers/{{0}}?page={CurrentPage}&pageSize={PageSize}";

        var queryableTask = client.GetFromJsonAsync<PagedWithMetricsViewModel<CustomerViewModel>>(string.Format(url, "queryable"));
        var enumerableTask = client.GetFromJsonAsync<PagedWithMetricsViewModel<CustomerViewModel>>(string.Format(url, "enumerable"));

        await Task.WhenAll(queryableTask, enumerableTask);

        QueryableResult = queryableTask.Result!;
        EnumerableResult = enumerableTask.Result!;

        return Page();
    }
}
