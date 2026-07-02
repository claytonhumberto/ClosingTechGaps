using ClosingTechGaps.Application.Interfaces;
using ClosingTechGaps.Application.Services;
using ClosingTechGaps.Domain.Repositories;
using ClosingTechGaps.Infrastructure.IndexDemo;
using ClosingTechGaps.Infrastructure.Persistence;
using ClosingTechGaps.Infrastructure.Persistence.Repositories;
using ClosingTechGaps.Infrastructure.Idempotency;
using ClosingTechGaps.Infrastructure.SqlInjectionDemo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ClosingTechGaps.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddDbContext<AppDbContext>(opt =>
            opt.UseInMemoryDatabase("ClosingTechGapsDb")
               .UseLazyLoadingProxies());

        services.AddScoped<QueryCounter>();
        services.AddScoped<SqlInjectionDemoService>();
        services.AddSingleton<IIdempotencyStore, InMemoryIdempotencyStore>();
        services.AddSingleton<IndexDemoService>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerService, CustomerService>();

        return services;
    }
}
