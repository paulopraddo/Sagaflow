namespace Sagaflow.Contracts.Catalog;

public sealed record ProductCreated(Guid ProductId, string Sku, string Name, decimal Price);

public sealed record ProductPriceChanged(Guid ProductId, decimal OldPrice, decimal NewPrice);
