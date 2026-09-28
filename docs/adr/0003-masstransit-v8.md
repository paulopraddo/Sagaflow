# ADR 0003 — MassTransit v8 on RabbitMQ

**Status:** accepted

## Context

The outbox, inbox, saga persistence, scheduling and retry policies could be written by hand on top of `RabbitMQ.Client`. That would be a large amount of infrastructure code, easy to get subtly wrong, and beside the point of the project: the business flow and its consistency guarantees.

MassTransit provides all of them, integrates with EF Core and OpenTelemetry, and ships a test harness. From v9 it requires a commercial license.

## Decision

Use **MassTransit 8.x** (open source, Apache 2.0), pinned below v9 in `Directory.Packages.props`, with the RabbitMQ transport. Saga timeouts and delayed redelivery use RabbitMQ's delayed-message exchange, which is bundled in the `masstransit/rabbitmq` image used both by Docker Compose and Testcontainers.

## Consequences

- The patterns stay visible in the code as configuration (`AddEntityFrameworkOutbox`, `UseEntityFrameworkOutbox`, `MassTransitStateMachine`), not hidden plumbing, and the ADRs explain *why* each is there.
- The v8 line will eventually stop receiving updates; moving to v9 (licensed) or another library is a contained change in `Sagaflow.Infrastructure`.
- Azure Service Bus would be a drop-in transport change (`UsingAzureServiceBus`). RabbitMQ was chosen because it runs locally and in CI at no cost.
