# ADR 0004 — Orders keeps its own copy of catalog prices

**Status:** accepted

## Context

To place an order, Orders needs each product's current price and name. Two options:

1. Call Catalog synchronously (HTTP) during checkout.
2. Keep a local read model in Orders, fed by `ProductCreated` and `ProductPriceChanged` events.

## Decision

Keep a local **`ProductSnapshot`** table in the Orders database, updated by a consumer of catalog events. Prices are always taken from it, never from the client.

## Consequences

- Checkout has no runtime dependency on Catalog: Catalog can be down or slow and orders still flow.
- Services stay decoupled at runtime. No service ever calls another over HTTP; every interaction goes through the broker.
- The snapshot is eventually consistent: a price change takes a moment to reach Orders, and a brand-new product can't be ordered until its event has been projected. That's acceptable for a catalog, and the integration tests account for it by retrying until the product is known.
- If a `ProductPriceChanged` overtakes its `ProductCreated`, the consumer throws and redelivery retries it later.
