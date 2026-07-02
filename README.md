# ClosingTechGaps

A practical .NET 10 project built to demonstrate mastery of technical concepts identified as gaps during a developer interview. Each feature in this solution was intentionally chosen to address a specific weakness — showing not just awareness of the problem, but hands-on implementation of the solution.

The project simulates a customer registration system with a REST API, an ASP.NET Razor Pages frontend, in-memory persistence, and full DDD layering.

---

## Tech Stack

- .NET 10 / ASP.NET Core 10
- Entity Framework Core 10 (In-Memory + Lazy Loading Proxies)
- SQLite + Microsoft.Data.Sqlite (SQL Injection & Index demos)
- Razor Pages + Bootstrap 5
- IHttpClientFactory, IAsyncActionFilter, ConcurrentDictionary, SemaphoreSlim

---

## Project Structure

```
ClosingTechGaps/
└── src/
    ├── ClosingTechGaps.API            # REST API — controllers, middleware, entry point
    ├── ClosingTechGaps.Application    # Use cases, DTOs, service interfaces
    ├── ClosingTechGaps.Domain         # Entities, Value Objects, repository contracts
    ├── ClosingTechGaps.Infrastructure # EF Core, repositories, seeder, DI registration
    └── ClosingTechGaps.Web            # Razor Pages frontend consuming the API
```

---

## Technical Gaps — What Was Studied, Why It Matters, How It Was Solved

### 1. Domain-Driven Design (DDD)

**The gap:** Difficulty structuring applications beyond a simple CRUD layout — mixing business rules with infrastructure concerns.

**The danger:** Without clear boundaries, the codebase becomes tightly coupled. A change in the database layer breaks business logic. Testing becomes nearly impossible because there is no separation between "what the system does" and "how it stores data."

**How it was solved:**
The solution is split into four projects, each with a clear responsibility:

| Layer | Responsibility |
|---|---|
| `Domain` | Entities (`Customer`), Value Objects (`Address`, `ContactInfo`), repository interfaces |
| `Application` | Use cases (`CustomerService`), DTOs, service interfaces |
| `Infrastructure` | EF Core `DbContext`, repository implementations, data seeding |
| `API` | HTTP controllers, middleware, dependency wiring |

Dependencies only flow inward — `Infrastructure` knows about `Domain`, but `Domain` knows nothing about `Infrastructure`. This is the core rule of DDD layering.

---

### 2. Value Objects

**The gap:** Treating every piece of data as a primitive (plain `string`, `int`) even when the data has its own rules and identity.

**The danger:** Address fields scattered across the `Customer` entity with no cohesion. Duplication of validation logic. No way to enforce that an address is always complete.

**How it was solved:**
`Address` and `ContactInfo` are modeled as C# `record` Value Objects inside the `Domain` layer. EF Core maps them as owned types (`OwnsOne`), keeping them in the same table row without a separate entity identity.

```csharp
public record Address(string Street, string City, string State, string ZipCode, string Country);
public record ContactInfo(string Email, string Phone);
```

---

### 3. Repository Pattern

**The gap:** Writing database queries directly inside controllers or service methods, making the persistence mechanism impossible to swap or test in isolation.

**The danger:** If the team decides to migrate from one ORM to another, or switch databases, every controller needs to change. Unit testing requires a real database connection.

**How it was solved:**
`ICustomerRepository` is defined in the `Domain` layer with no knowledge of EF Core. `CustomerRepository` in `Infrastructure` implements it using `DbContext`. The `Application` layer only depends on the interface — it never sees EF Core.

---

### 4. Pagination

**The gap:** Returning all records from an endpoint with no limit — a pattern that works in demos but fails in production with real data volumes.

**The danger:** A table with 2,000 rows is manageable. With 200,000 rows, an unbounded `GET /customers` will time out, exhaust memory, and degrade the entire service for all users simultaneously.

**How it was solved:**
A dedicated `GET /api/customers/paged?page=1&pageSize=20` endpoint was added. The repository executes `Skip/Take` directly in the database query. The response includes full pagination metadata:

```json
{
  "items": [...],
  "page": 1,
  "pageSize": 20,
  "totalCount": 2000,
  "totalPages": 100,
  "hasPreviousPage": false,
  "hasNextPage": true
}
```

The frontend consumes this metadata to render a complete paginator with first/previous/numbered/next/last controls.

**Edge case handled:** If the requested page number exceeds `TotalPages`, the API returns `400 Bad Request`. The Web frontend intercepts this and automatically redirects the user to the last valid page instead of rendering a broken empty table.

---

### 5. SQL Injection Prevention

**The gap:** Uncertainty about when and how SQL Injection vulnerabilities are introduced, and what the framework does or does not protect against by default.

**The danger:** SQL Injection is consistently ranked in the OWASP Top 10. A single unsanitized string concatenated into a SQL query can expose, corrupt, or delete an entire database — and do it silently, leaving no application-level error.

**How it was solved — three layers of defense:**

**Layer 1 — ORM parameterization (automatic):**
Every query in this project goes through EF Core LINQ. The framework always generates parameterized SQL (`WHERE Name = @p0`), making classic injection impossible as long as raw string interpolation is avoided.

**Layer 2 — Input validation on DTOs:**
All API inputs are validated before reaching the service layer using `DataAnnotations`:

```csharp
public record CreateCustomerDto(
    [Required, StringLength(200, MinimumLength = 2)] string Name,
    DateOnly BirthdayDate,
    [Required] AddressDto Address,
    [Required] ContactInfoDto ContactInfo
);

public record ContactInfoDto(
    [Required, EmailAddress, StringLength(200)] string Email,
    [Required, StringLength(30)] string Phone
);

public record AddressDto(
    [Required, RegularExpression(@"^\d{5}-\d{3}$")] string ZipCode,
    ...
);
```

**Layer 3 — Global Exception Middleware:**
A `GlobalExceptionMiddleware` catches all unhandled exceptions before they reach the HTTP response. In production, the client receives only a generic message — never a stack trace, SQL error detail, or internal path that could aid an attacker in mapping the system.

```csharp
// In development: real message
// In production: "Ocorreu um erro interno. Contate o suporte."
var message = env.IsDevelopment() ? ex.Message : "An internal error occurred.";
```

---

### 6. In-Memory Database for Development

**The gap:** Dependency on a local database installation as a prerequisite to running any part of the project — a friction point that slows onboarding and makes demos unreliable.

**The danger:** "It works on my machine" scenarios caused by database version mismatches, missing migrations, or incorrect connection strings. New developers waste hours on setup before writing a single line of business code.

**How it was solved:**
EF Core's `UseInMemoryDatabase` is configured in `DependencyInjection.cs`. On startup, a `CustomerSeeder` automatically inserts 2,000 randomized customer records. The project runs with a single `dotnet run` — no database engine required.

---

### 7. HttpClient Best Practices (IHttpClientFactory)

**The gap:** Instantiating `new HttpClient()` directly inside services or page models — a common mistake that causes socket exhaustion under load.

**The danger:** Each `new HttpClient()` holds a socket open even after disposal due to the way TCP connections are pooled. Under sustained traffic, the application runs out of available sockets and starts throwing `SocketException`, taking the service down.

**How it was solved:**
`IHttpClientFactory` is registered in `Program.cs` with a named client `"CustomerApi"`. The factory manages connection pooling and lifetime correctly. The Razor Page model receives the factory via constructor injection and creates a scoped client per request.

```csharp
builder.Services.AddHttpClient("CustomerApi", client =>
{
    client.BaseAddress = new Uri(config["ApiSettings:BaseUrl"]);
});
```

---

### 8. IQueryable vs IEnumerable with Entity Framework

**The gap:** Not knowing the difference between `IQueryable<T>` and `IEnumerable<T>` when querying data through Entity Framework — and using them interchangeably without understanding the performance implications.

**The danger:** When you break an `IQueryable` chain into `IEnumerable` too early (for example, by calling `.ToList()` or `.AsEnumerable()` before applying filters), Entity Framework executes the query immediately and loads **every row from the table into memory**. All subsequent filtering, ordering, and pagination then happen in C# instead of in the database. With 2,000 rows the impact is manageable. With 200,000 or 2,000,000 rows, this pattern exhausts memory, spikes CPU, and can take down the entire service.

**The core difference:**

| | `IQueryable<T>` | `IEnumerable<T>` |
|---|---|---|
| Execution | Deferred — builds a SQL expression tree | Immediate — runs when enumerated |
| Where filtering happens | **Database** | **C# / Memory** |
| Rows transferred from DB | Only what's needed | **All rows** |
| Safe for pagination | Yes | No — loads everything first |
| EF provider awareness | Yes — translates LINQ to SQL | No — EF is done, C# takes over |

**How it was solved — live side-by-side comparison:**

Two dedicated repository methods were implemented for the same paginated query:

```csharp
// IQueryable — the entire pipeline is SQL
IQueryable<Customer> query = context.Customers.OrderBy(c => c.Name);
var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
// SQL: SELECT ... ORDER BY Name OFFSET N ROWS FETCH NEXT 20 ROWS ONLY
// Rows loaded into memory: 20

// IEnumerable — ToList() fires immediately, all rows come first
IEnumerable<Customer> allInMemory = await context.Customers.ToListAsync();
var items = allInMemory.OrderBy(c => c.Name).Skip((page - 1) * pageSize).Take(pageSize);
// SQL: SELECT * FROM Customers
// Rows loaded into memory: 2,000
```

A dedicated comparison page at `/Customers/Comparison` calls both endpoints in parallel and renders the results side by side with live performance metrics: execution time in milliseconds, rows loaded into memory, and an explanation of the SQL generated by each strategy. The data returned to the user is identical — the difference is entirely in the cost paid to get it.

---

### 9. Lazy Loading vs Eager Loading — The N+1 Problem

**The gap:** Not knowing the difference between lazy loading and eager loading in Entity Framework, and using lazy loading without awareness of the database round-trips it generates.

**The danger:** Lazy loading silently fires one additional SQL query for every navigation property accessed on every entity in a loop. Loading a page of 20 customers and then accessing `.Orders` on each one produces **22 database queries** instead of 2. This is the classic **N+1 problem**. With a real database and network latency, each extra query costs 1–5ms — meaning 20 extra queries per request adds up to 100ms of hidden overhead, and the problem scales linearly with page size.

**The core difference:**

| | Lazy Loading | Eager Loading |
|---|---|---|
| How it works | Navigation properties are loaded on first access | Related data is joined in the initial query via `Include` |
| Queries fired (20 customers + orders) | **22** (1 customers + 1 COUNT + 1 per customer) | **2** (1 JOIN query + 1 COUNT) |
| Requires | `UseLazyLoadingProxies()` + `virtual` nav properties | `.Include(c => c.Orders)` |
| Risk | N+1 problem — invisible until profiled | None — predictable single round-trip |

**How it was solved:**

A new `Order` entity was added with a `virtual ICollection<Order>` on `Customer`, enabling lazy loading via EF Core Proxies (`UseLazyLoadingProxies()`). Two repository methods were implemented for the same query:

```csharp
// Lazy Loading — N+1: one extra query per customer
var customers = await context.Customers.OrderBy(c => c.Name).Skip(...).Take(20).ToListAsync();
foreach (var customer in customers)
    _ = customer.Orders.Count; // fires a new SELECT for each customer

// Eager Loading — single JOIN query
var customers = await context.Customers
    .Include(c => c.Orders)           // <-- tells EF to JOIN now
    .OrderBy(c => c.Name).Skip(...).Take(20).ToListAsync();
foreach (var customer in customers)
    _ = customer.Orders.Count; // already in memory, zero extra queries
```

A live comparison page at `/Customers/LoadingComparison` runs both strategies in parallel and shows:
- **DB queries executed** by each strategy
- **Execution time** in milliseconds
- The code snippet for each approach
- A real table of customers with their orders, total spent, and last order date — proving the data is identical, only the cost differs

Changing the page size selector from 20 to 50 makes the N+1 count grow visibly from 22 to 52 queries in real time.

---

### 10. Idempotency

**The gap:** Not protecting POST endpoints against double-submission — a user double-clicking a button, a mobile app retrying after a timeout, or a message queue delivering the same event twice can each create duplicate records silently.

**The danger:** Financial systems process duplicate charges. Order systems ship the same order twice. User registration systems create duplicate accounts. These bugs are intermittent, hard to reproduce, and expensive to clean up after the fact.

**How it was solved:**

A dedicated `POST /api/customers/idempotent` endpoint is protected by an `IAsyncActionFilter` that reads an `Idempotency-Key` header (a client-generated UUID):

1. If the key is **new**: the request executes normally and the response is stored in an `InMemoryIdempotencyStore` (a `ConcurrentDictionary` Singleton).
2. If the key is **already seen**: the stored response is returned immediately — the database is never touched.
3. A `SemaphoreSlim` per key serializes concurrent requests with the **same key**, preventing the race condition where two simultaneous requests both pass the "new key" check before either one stores the response.

```csharp
// Client sends the same key on retry
POST /api/customers/idempotent
Idempotency-Key: 550e8400-e29b-41d4-a716-446655440000

// First call  → 201 Created  (record persisted)
// Second call → 201 Created  (X-Idempotency-Replayed: true, no DB write)
// Third call  → 201 Created  (X-Idempotency-Replayed: true, no DB write)
```

The demo page fires **5 simultaneous requests** to both endpoints. The unprotected one creates 5 duplicate records; the idempotent one creates exactly 1.

---

### 11. Database Index Types

**The gap:** Not knowing when to create an index, which type to use, or what the performance difference actually looks like — leading to either missing indexes (slow queries) or over-indexing (slow writes, wasted storage).

**The danger:** A table scan on a 10-million-row table can take seconds and lock resources for every other query running concurrently. Using the wrong index type (e.g. a full index where a filtered one suffices) wastes memory and increases write overhead. Not using a covering index forces an extra random I/O read per row returned.

**How it was solved — 7 types demonstrated live against 100,000 rows in SQLite:**

| Type | Use | Demo |
|---|---|---|
| **Clustered** | Physically orders the table by PK | PK lookup (O log n) vs full description scan (O n) |
| **Non-Clustered** | Fast lookup without reordering | `SCAN TABLE` → `SEARCH USING INDEX` in EXPLAIN QUERY PLAN |
| **Unique** | Integrity at the database level | Live INSERT rejected with `UNIQUE constraint failed` |
| **Covering** | Avoids table lookup — index holds all needed columns | Index-only scan vs scan + table lookup, timing comparison |
| **Filtered** | Partial index on a subset of rows | Index on active rows only — smaller, faster, lower write cost |
| **Full-text** | Tokenized inverted index for keyword search | `LIKE '%term%'` (full scan) vs `FTS5 MATCH` (inverted index) |
| **Columnstore** | Column-oriented storage for analytics/BI | Conceptual with SQL Server syntax — not available in SQLite |

Each demo shows the raw `EXPLAIN QUERY PLAN` output from SQLite alongside a timing comparison, making the engine's decision visible rather than theoretical.

---

## Running Locally

No database required. Start both projects:

```bash
# Terminal 1 — REST API (https://localhost:7121)
dotnet run --project src/ClosingTechGaps.API --launch-profile https

# Terminal 2 — Web Frontend (https://localhost:7187)
dotnet run --project src/ClosingTechGaps.Web --launch-profile https
```

Then open **https://localhost:7187/Customers** to see the paginated customer list.

The API swagger/OpenAPI schema is available at **https://localhost:7121/openapi/v1.json** in Development mode.

---

## Seeded Data

On every startup, 2,000 random customers are inserted into the in-memory database with realistic Brazilian names, addresses, birth dates, emails, and phone numbers. The data is regenerated fresh on each run.
