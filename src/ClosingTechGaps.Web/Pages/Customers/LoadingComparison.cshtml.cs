using System.Net.Http.Json;
using ClosingTechGaps.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClosingTechGaps.Web.Pages.Customers;

public class LoadingComparisonModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public LoadingComparisonViewModel<CustomerWithOrdersViewModel> LazyResult { get; private set; } = default!;
    public LoadingComparisonViewModel<CustomerWithOrdersViewModel> EagerResult { get; private set; } = default!;

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

        var lazyTask = client.GetFromJsonAsync<LoadingComparisonViewModel<CustomerWithOrdersViewModel>>(string.Format(url, "lazy"));
        var eagerTask = client.GetFromJsonAsync<LoadingComparisonViewModel<CustomerWithOrdersViewModel>>(string.Format(url, "eager"));

        await Task.WhenAll(lazyTask, eagerTask);

        LazyResult = lazyTask.Result!;
        EagerResult = eagerTask.Result!;

        return Page();
    }
}
