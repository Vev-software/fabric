# Fabric Atlas Discovery Events

This document records the public Atlas discovery asset lifecycle event surface implemented for
`fabric#35`.

## Scope

Fabric owns the public cross-product contract for discovery-driven Atlas asset lifecycle events:

- stable event names
- tenant, correlation and scanner-enrollment references
- redaction-safe metadata shape
- schema + SDK mirrors for subscribers

Fabric does **not** own Atlas catalogue mutation logic, Atlas asset schemas, or the policy for when
Atlas emits these events.

## Boundary

These events are intentionally narrower than a generic Atlas event model. They exist so Atlas
discovery writes can publish one public seam without turning Fabric into an Atlas domain host.

The event tells a subscriber:

- which tenant boundary the change happened within
- which discovery enrollment / principal caused it
- which Atlas asset id resulted
- which discovery source agent and observed id it came from
- which stable public event name was emitted

It does **not** embed the resulting Atlas asset document or any customer content.

## Event names

The public event-type vocabulary is:

- `eu.vev.atlas.server.created.v1`
- `eu.vev.atlas.server.updated.v1`
- `eu.vev.atlas.application.created.v1`
- `eu.vev.atlas.application.updated.v1`

Atlas owns the decision of whether a reconciliation outcome is a `created` or `updated` effect.
Fabric owns the names so subscribers do not depend on per-service strings.

## Contract

`AtlasDiscoveryAssetLifecycleEvent` includes:

- `eventId`
- `occurredAt`
- `tenant`
- `enrollmentId`
- `principalId`
- `source`
- `eventType`
- `assetId`
- `sourceAgentId`
- `observedId`
- `correlationId`
- `capability`
- optional `metadata`

`capability` is fixed to `atlas.discovery.ingestion`, because these events exist only for the
discovery apply path.

## Metadata discipline

`metadata` is string-valued and optional. It may carry small redaction-safe hints such as
`assetKind`, `change`, or a reconciliation note. It must never carry secrets, raw payloads,
customer content, or a serialized Atlas asset document.

## Relationship to audit

Audit and events stay separate:

- audit uses the append-only `AuditEvent` envelope for durable who-did-what records
- lifecycle events are for subscribers, orchestration and timeline stitching

Products should emit both when discovery effects are material:

- audit through `DiscoveryAuditVocabulary`
- public lifecycle event through `AtlasDiscoveryEventVocabulary`
