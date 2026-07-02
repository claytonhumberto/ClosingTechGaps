using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ClosingTechGaps.Web.Pages.SqlInjectionDemo;

public record DemoCustomer(int Id, string Name, string Email, string Role);
public record SqlDemoResult(string SqlExecuted, IEnumerable<DemoCustomer> Records, bool DataLeaked, string Warning);

public class IndexModel(IHttpClientFactory httpClientFactory) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string SearchTerm { get; set; } = string.Empty;

    public SqlDemoResult? UnsafeResult { get; private set; }
    public SqlDemoResult? SafeResult { get; private set; }
    public bool Searched { get; private set; }

    public async Task OnGetAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchTerm)) return;

        Searched = true;
        var client = httpClientFactory.CreateClient("CustomerApi");
        var encoded = Uri.EscapeDataString(SearchTerm);

        var unsafeTask = client.GetFromJsonAsync<SqlDemoResult>($"/api/demo/sql-injection/unsafe?name={encoded}");
        var safeTask   = client.GetFromJsonAsync<SqlDemoResult>($"/api/demo/sql-injection/safe?name={encoded}");

        await Task.WhenAll(unsafeTask, safeTask);

        UnsafeResult = unsafeTask.Result;
        SafeResult   = safeTask.Result;
    }
}
