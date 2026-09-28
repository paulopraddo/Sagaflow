using System.ComponentModel.DataAnnotations;
using MassTransit;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sagaflow.Catalog.Api.Data;
using Sagaflow.Catalog.Api.Domain;
using Sagaflow.Contracts.Catalog;

namespace Sagaflow.Catalog.Api.Endpoints;

public static class ProductEndpoints
{
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder app)
    {
        var products = app.MapGroup("/products").WithTags("Products");

        products.MapGet("/", ListProducts);
        products.MapGet("/{id:guid}", GetProduct).WithName(nameof(GetProduct));
        products.MapPost("/", CreateProduct);
        products.MapPut("/{id:guid}/price", ChangePrice);

        return app;
    }

    private static async Task<Ok<List<ProductResponse>>> ListProducts(CatalogDbContext db, CancellationToken ct) =>
        TypedResults.Ok(await db.Products
            .AsNoTracking()
            .OrderBy(p => p.Name)
            .Select(p => ProductResponse.From(p))
            .ToListAsync(ct));

    private static async Task<Results<Ok<ProductResponse>, NotFound>> GetProduct(Guid id, CatalogDbContext db, CancellationToken ct) =>
        await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct) is { } product
            ? TypedResults.Ok(ProductResponse.From(product))
            : TypedResults.NotFound();

    private static async Task<Results<CreatedAtRoute<ProductResponse>, Conflict<ProblemDetails>>> CreateProduct(
        CreateProductRequest request,
        CatalogDbContext db,
        IPublishEndpoint publisher,
        TimeProvider clock,
        CancellationToken ct)
    {
        var product = Product.Create(request.Sku, request.Name, request.Description, request.Price, clock);
        db.Products.Add(product);

        // With the bus outbox enabled this does not touch RabbitMQ: the message is written to the
        // OutboxMessage table and committed atomically with the product by SaveChangesAsync.
        await publisher.Publish(new ProductCreated(product.Id, product.Sku, product.Name, product.Price), ct);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return TypedResults.Conflict(new ProblemDetails
            {
                Title = "SKU already exists",
                Detail = $"A product with SKU '{product.Sku}' already exists.",
                Status = StatusCodes.Status409Conflict,
            });
        }

        return TypedResults.CreatedAtRoute(ProductResponse.From(product), nameof(GetProduct), new { id = product.Id });
    }

    private static async Task<Results<Ok<ProductResponse>, NotFound>> ChangePrice(
        Guid id,
        ChangePriceRequest request,
        CatalogDbContext db,
        IPublishEndpoint publisher,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (await db.Products.FirstOrDefaultAsync(p => p.Id == id, ct) is not { } product)
        {
            return TypedResults.NotFound();
        }

        var oldPrice = product.Price;
        product.ChangePrice(request.Price, clock);

        await publisher.Publish(new ProductPriceChanged(product.Id, oldPrice, product.Price), ct);
        await db.SaveChangesAsync(ct);

        return TypedResults.Ok(ProductResponse.From(product));
    }
}

public sealed record CreateProductRequest(
    [property: Required, StringLength(64, MinimumLength = 3)] string Sku,
    [property: Required, StringLength(200, MinimumLength = 1)] string Name,
    [property: StringLength(2000)] string? Description,
    [property: Range(0.01, 1_000_000)] decimal Price);

public sealed record ChangePriceRequest([property: Range(0.01, 1_000_000)] decimal Price);

public sealed record ProductResponse(Guid Id, string Sku, string Name, string? Description, decimal Price, DateTimeOffset UpdatedAt)
{
    public static ProductResponse From(Product p) => new(p.Id, p.Sku, p.Name, p.Description, p.Price, p.UpdatedAt);
}
