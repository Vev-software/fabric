# Fabric Audit

This document records the current public Fabric audit event surface implemented for `fabric#6`.

## Scope

Audit is cross-product and dangerous to do inconsistently, so it is Fabric from day one
(`05 §5`, `11 §4`). Fabric owns:

- the append-only `AuditEvent` envelope every product create/edit/delete flows through
- the redaction rules that keep secrets and customer content out of the audit trail
- the correlation fields that let product and substrate events stitch into one request story
- a simple append-only reference sink for development and tests

Fabric does **not** own the audit store, retention policy, export pipeline or a product's
action and resource vocabulary.

## Boundary

The envelope carries **who did what to which resource, in which tenant, when, and under which
correlation id** — never the product-domain schema behind those values. A product supplies
`action` and `resource` *values* (for example `atlas.catalogue.write` and
`atlas:catalogue/main`); it never extends the envelope shape (`05 §3`).

## Envelope

`AuditEvent` fields:

- `eventId`: stable unique identifier for the record; the append-only primary key.
- `occurredAt`: when the audited action occurred.
- `tenant`: the tenant whose isolation boundary the action happened within.
- `actor`: a redaction-safe projection of the acting principal (`principalId`, `displayName`,
  `roles`) — see redaction below.
- `source`: the emitting component, e.g. `atlas` or `fabric.control-plane`.
- `action`: the product-supplied action value.
- `resource`: the resource the action targeted (`value`, optional `type`).
- `category`: `Data`, `Admin` or `Security`. Admin and security events are immutable.
- `outcome`: `Success`, `Failure` or `Denied`, so a reviewer can tell an attempt from an effect.
- `correlationId`: correlates every event emitted while handling one request.
- `causationId` *(optional)*: the event that caused this one, for causal stitching.
- `metadata` *(optional)*: string-valued, opaque product context — never secrets or customer content.

## Append-only and immutability

The audit trail is append-only. The reference sink `InMemoryAuditLog` exposes only `Append` and a
read-only view; it has no update or delete surface.

`AuditEvent.IsImmutable` is true for `Admin` and `Security` events. These must never be edited,
redacted or tombstoned — even under a later data-subject erasure request — because they are the
record of who changed access and security posture.

## Redaction

Redaction is baked into the contract, not left to each caller (`03 · E4/E5`, `AGENTS.md §1.6`):

- The **actor is a projection**, not the raw principal. `AuditActor.FromPrincipal` copies the
  subject id, display name and roles but drops `PrincipalContext.Claims`, so email and other
  provider claims never reach the audit trail.
- `metadata` is **string-valued and structurally checked**. `AuditRedaction.IsRedactionSafe`
  rejects keys that look like secrets (`password`, `token`, `apiKey`, `authorization`,
  `credential`, `sessionId`, …, separator- and case-insensitive). `InMemoryAuditLog.Append`
  runs the check and throws `AuditRedactionException` rather than persist a suspect payload.

The check guards keys, not values, so products still keep sensitive values out of metadata — but
the common mistakes fail loudly instead of leaking silently.

## Correlation

`correlationId` is shared by every event emitted while handling one request, across products and
the substrate, so a single request's story can be reassembled. `causationId` optionally links an
event to the one that caused it. This aligns with the event envelope conventions (CloudEvents,
`05 §2 Events`): Fabric-owned envelope, product-owned values.

## Public API shapes

The implemented public contract includes:

- `AuditEvent`
- `AuditActor` (with `AuditActor.FromPrincipal`)
- `AuditResource`
- `AuditCategory`, `AuditOutcome`
- `AuditRedaction`, `AuditRedactionException`
- `IAuditSink`, `InMemoryAuditLog`

Schemas live in `schemas/v1/audit-event.schema.json`. A representative sample document lives in
`conformance/samples/audit-event.sample.json`. The TypeScript SDK mirrors the shapes in
`sdk/typescript/src/index.ts`.

## Usage

```csharp
IAuditSink audit = new InMemoryAuditLog();

audit.Append(new AuditEvent(
    EventId: Guid.NewGuid().ToString(),
    OccurredAt: DateTimeOffset.UtcNow,
    Tenant: new TenantContext("tenant-a"),
    Actor: AuditActor.FromPrincipal(principal),
    Source: "atlas",
    Action: "atlas.catalogue.write",
    Resource: new AuditResource("atlas:catalogue/main", "catalogue-entry"),
    Category: AuditCategory.Data,
    Outcome: AuditOutcome.Success,
    CorrelationId: correlationId));
```

The product supplies action and resource values and emits through the envelope; Fabric owns the
append-only, redaction-checked guarantee.

## Discovery-specific vocabulary

For Atlas discovery enrollment and ingestion flows, use the shared values in
`Vev.Fabric.Contracts.Discovery.DiscoveryAuditVocabulary` rather than inventing per-service
strings. The audit envelope stays generic; Fabric simply seeds stable discovery action/resource
values so lifecycle and security reviews can join events reliably.
