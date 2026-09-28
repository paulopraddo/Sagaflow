# ADR 0001 — Orchestrated saga for the order flow

**Status:** accepted

## Context

Placing an order touches three services, each with its own database: stock must be reserved (Inventory), the customer charged (Payments) and the order settled (Orders). A distributed transaction (2PC) across them is not an option: RabbitMQ doesn't take part in one, and it would couple the services' availability. The flow therefore needs a saga: a sequence of local transactions where every step that can be undone has a compensating action.

A saga can be **choreographed**, with each service reacting to the others' events, or **orchestrated**, with one component telling the others what to do.

## Decision

Use an **orchestrated** saga: a MassTransit state machine (`OrderStateMachine`) in the Orders service. It sends commands to point-to-point queues and reacts to the events the participants publish.

## Consequences

- The whole business process, including compensations (`ReleaseStock`, `RefundPayment`) and the payment timeout, lives in one readable file and is unit-testable with the in-memory harness.
- Inventory and Payments stay simple: they execute commands and announce outcomes, without knowing about orders' lifecycle.
- Orders depends on the participants' command contracts. That coupling is acceptable because Orders owns the process.
- The orchestrator is a single point of logic, though not of availability: its state is persisted, so any instance can resume a saga.
- Choreography was rejected because the flow ("who reacts to `PaymentFailed`?") would be spread across services and harder to follow as steps are added.
