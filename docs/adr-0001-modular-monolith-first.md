# ADR-0001: Start as a modular monolith, extract services only for a measured reason

- **Status:** Accepted
- **Date:** 2026-11-19

## Context

A small team (around six developers) with one product and one release schedule. Splitting into many
microservices that share one database gives the costs of distribution (network failures, versioning,
distributed debugging, more infrastructure) without independent deployability.

## Decision

- One deployable host. Each business capability is a module with a public `Contracts` project and an
  `internal` implementation.
- Modules talk through contracts (synchronous queries) and integration events (facts). They never
  share tables. In a real database each module owns its own schema.
- Boundaries are enforced by architecture tests in CI, not by convention.

## When to extract a module into a service

Extract only when at least one of these is true and measured:

| Driver | Evidence |
|---|---|
| Independent scaling | The module's load profile differs by an order of magnitude and scaling the whole host is costly |
| Independent release cadence | A team is blocked by others' release cycle more than occasionally |
| Isolation | Security, compliance or blast-radius requirements (e.g. PCI scope, PHI) |
| Team ownership | A team is ready to own it end to end, including on-call |

## Extraction path

1. The module already has no shared tables. Move its schema to its own database.
2. Replace the in-process `ICatalogApi` implementation with an HTTP/gRPC client in the callers' host.
3. Replace `InProcessEventBus` with an outbox and a broker for that module's events.
4. Move `XModule` into its own host. The callers' code doesn't change.

## Consequences

- Simple operations today: one deploy, one process, in-process calls, easy debugging.
- The design keeps the option to extract later at low cost.
- The discipline is enforced automatically by tests.
