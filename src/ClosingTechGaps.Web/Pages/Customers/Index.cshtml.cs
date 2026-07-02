using System.Net;
using System.Net.Http.Json;
using ClosingTechGaps.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClosingTechGaps.Web.Pages.Customers;

public class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    public PagedResultViewModel<CustomerViewModel> Customers { get; private set; } = default!;

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 20;

    public async Task<IActionResult> OnGetAsync()
    {
        if (CurrentPage < 1) return RedirectToPage(new { currentPage = 1, pageSize = PageSize });
        if (PageSize < 1 || PageSize > 100) return RedirectToPage(new { currentPage = CurrentPage, pageSize = 20 });

        var client = httpClientFactory.CreateClient("CustomerApi");
        var response = await client.GetAsync($"/api/customers/paged?page={CurrentPage}&pageSize={PageSize}");

        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            // Page exceeds total — redirect to the last valid page
            var fallback = await client.GetFromJsonAsync<PagedResultViewModel<CustomerViewModel>>(
                $"/api/customers/paged?page=1&pageSize={PageSize}"
            );
            int lastPage = fallback?.TotalPages ?? 1;
            return RedirectToPage(new { currentPage = lastPage, pageSize = PageSize });
        }

        response.EnsureSuccessStatusCode();
        Customers = (await response.Content.ReadFromJsonAsync<PagedResultViewModel<CustomerViewModel>>())!;
        return Page();
    }
}
