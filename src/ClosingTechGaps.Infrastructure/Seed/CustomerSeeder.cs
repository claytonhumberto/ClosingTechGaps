using ClosingTechGaps.Domain.Entities;
using ClosingTechGaps.Domain.Repositories;
using ClosingTechGaps.Domain.ValueObjects;
using ClosingTechGaps.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ClosingTechGaps.Infrastructure.Seed;

public static class CustomerSeeder
{
    private static readonly string[] FirstNames =
    [
        "Ana", "Carlos", "Mariana", "Felipe", "Juliana", "Roberto", "Fernanda", "Lucas",
        "Patrícia", "Diego", "Beatriz", "Gustavo", "Camila", "Rafael", "Larissa", "Eduardo",
        "Vanessa", "Thiago", "Aline", "Bruno", "Cristina", "Henrique", "Daniela", "Marcos",
        "Renata", "André", "Tatiana", "Leonardo", "Priscila", "Rodrigo"
    ];

    private static readonly string[] LastNames =
    [
        "Silva", "Santos", "Oliveira", "Souza", "Rodrigues", "Ferreira", "Alves", "Pereira",
        "Lima", "Gomes", "Costa", "Ribeiro", "Martins", "Carvalho", "Almeida", "Lopes",
        "Sousa", "Fernandes", "Vieira", "Barbosa", "Rocha", "Dias", "Nascimento", "Andrade",
        "Moreira", "Nunes", "Marques", "Machado", "Mendes", "Freitas"
    ];

    private static readonly string[] Streets =
    [
        "Rua das Flores", "Av. Paulista", "Rua XV de Novembro", "Av. Brasil", "Rua Dom Pedro",
        "Rua Sete de Setembro", "Av. Atlântica", "Rua das Acácias", "Av. Independência",
        "Rua das Palmeiras", "Rua Tiradentes", "Av. Getúlio Vargas", "Rua da Liberdade",
        "Av. das Nações", "Rua Castelo Branco"
    ];

    private static readonly string[] Cities =
    [
        "São Paulo", "Rio de Janeiro", "Belo Horizonte", "Curitiba", "Porto Alegre",
        "Salvador", "Fortaleza", "Recife", "Manaus", "Belém", "Goiânia", "Florianópolis",
        "Vitória", "Natal", "João Pessoa"
    ];

    private static readonly string[] States =
    [
        "SP", "RJ", "MG", "PR", "RS", "BA", "CE", "PE", "AM", "PA", "GO", "SC", "ES", "RN", "PB"
    ];

    private static readonly string[] EmailDomains =
    [
        "gmail.com", "hotmail.com", "outlook.com", "yahoo.com.br", "uol.com.br"
    ];

    private static readonly string[] Products =
[
    "Notebook", "Monitor", "Teclado", "Mouse", "Headset", "Webcam",
    "SSD", "Memória RAM", "Placa de Vídeo", "Impressora", "Roteador", "Cabo HDMI"
];

public static async Task SeedAsync(ICustomerRepository repository, int count = 2000, CancellationToken ct = default)
    {
        var random = new Random(42);
        var customers = new List<Customer>(count);

        for (int i = 0; i < count; i++)
        {
            var firstName = FirstNames[random.Next(FirstNames.Length)];
            var lastName = LastNames[random.Next(LastNames.Length)];
            var name = $"{firstName} {lastName}";

            var birthday = DateOnly.FromDateTime(
                DateTime.Now.AddDays(-random.Next(365 * 18, 365 * 70))
            );

            var street = $"{Streets[random.Next(Streets.Length)]}, {random.Next(1, 2000)}";
            var city = Cities[random.Next(Cities.Length)];
            var state = States[random.Next(States.Length)];
            var zipCode = $"{random.Next(10000, 99999)}-{random.Next(100, 999)}";
            var address = new Address(street, city, state, zipCode, "Brasil");

            var emailLocal = $"{firstName.ToLower().Normalize()}{random.Next(1, 9999)}";
            var email = $"{emailLocal}@{EmailDomains[random.Next(EmailDomains.Length)]}";
            var phone = $"({random.Next(11, 99)}) 9{random.Next(1000, 9999)}-{random.Next(1000, 9999)}";
            var contactInfo = new ContactInfo(email, phone);

            customers.Add(new Customer(name, birthday, address, contactInfo));
        }

        await repository.AddRangeAsync(customers, ct);
    }

    public static async Task SeedOrdersAsync(AppDbContext context, int count = 2000, CancellationToken ct = default)
    {
        if (await context.Orders.AnyAsync(ct)) return;

        var random = new Random(42);
        var customerIds = await context.Customers.Select(c => c.Id).ToListAsync(ct);
        var orders = new List<Order>();

        foreach (var customerId in customerIds)
        {
            int orderCount = random.Next(0, 6); // 0 to 5 orders per customer
            for (int i = 0; i < orderCount; i++)
            {
                var product = Products[random.Next(Products.Length)];
                var amount = Math.Round((decimal)(random.NextDouble() * 4900 + 100), 2);
                var createdAt = DateTime.UtcNow.AddDays(-random.Next(1, 730));
                orders.Add(new Order(customerId, $"Compra de {product}", amount, createdAt));
            }
        }

        await context.Orders.AddRangeAsync(orders, ct);
        await context.SaveChangesAsync(ct);
    }
}
