# ADR 0002 — Transactional outbox and inbox for every service

**Status:** accepted

## Context

Every step of the flow both changes local state and emits a message: Catalog saves a product and announces it, Inventory reserves stock and reports it. Doing both naively creates a dual-write problem:

- commit, then publish: a crash in between loses the message;
- publish, then commit: a failed commit announces something that never happened.

RabbitMQ also delivers at least once, so consumers will eventually see duplicates.

## Decision

Use MassTransit's Entity Framework Core **transactional outbox** in every service, backed by that service's own PostgreSQL database:

- **Bus outbox** for API endpoints: `Publish` writes to `OutboxMessage` and the row commits together with the business change. A background relay delivers it afterwards.
- **Consumer inbox/outbox** on every receive endpoint: each consumed message is recorded in `InboxState` (deduplication), and outgoing messages are only dispatched after the consumer's transaction commits.

Where a duplicate can arrive with a *new* message id, such as a re-sent command, handlers also rely on natural idempotency keys: one `StockReservation` per order and one `Payment` per order.

## Consequences

- A broker outage delays messages but never loses them; this is demonstrated in the README by stopping RabbitMQ mid-flow.
- Handlers produce exactly-once *effects* on top of at-least-once delivery.
- Extra database work per message (inbox lock, outbox rows, delivery bookkeeping) and a small relay delay (`QueryDelay` = 1s).
- The outbox tables live in each service's schema and are created by that service's migrations.
