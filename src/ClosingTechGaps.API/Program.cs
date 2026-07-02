using ClosingTechGaps.API.Filters;
using Scalar.AspNetCore;
using ClosingTechGaps.API.Middleware;
using ClosingTechGaps.Domain.Repositories;
using ClosingTechGaps.Infrastructure;
using ClosingTechGaps.Infrastructure.Seed;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWeb", policy =>
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .WithExposedHeaders("X-Idempotency-Replayed", "X-Idempotency-Key", "X-Idempotency-Created-At"));
});

builder.Services.AddControllers()
    .AddJsonOptions(opt =>
        opt.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles);
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure();
builder.Services.AddScoped<IdempotencyFilter>();

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseCors("AllowWeb");

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "ClosingTechGaps API";
        options.Theme = ScalarTheme.Purple;
    });
}

app.UseHttpsRedirection();
app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var repository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
    await CustomerSeeder.SeedAsync(repository, count: 2000);

    var dbContext = scope.ServiceProvider.GetRequiredService<ClosingTechGaps.Infrastructure.Persistence.AppDbContext>();
    await CustomerSeeder.SeedOrdersAsync(dbContext);
}

app.Run();
