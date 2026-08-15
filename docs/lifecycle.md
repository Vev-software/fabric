# Fabric Lifecycle

This document records the current public Fabric tenant lifecycle surface implemented for `fabric#8`.

## Scope

Fabric owns:

- the public lifecycle state model for hosted-capable tenants
- the timestamp contract products consume
- query and transition payloads
- a shared state machine that validates lifecycle progression

Fabric does **not** own schedulers, billing adapters, purge jobs or product-specific retention workflows.

## Canonical states

The hosted lifecycle is explicit and ordered:

`TrialActive` -> `TrialExpired` -> `ReadOnly` -> `Locked` -> `RetentionPeriod` -> `DataPurged`

The product runtime consumes the resulting state and reason code; it does not infer lifecycle from local booleans.

## Timestamp semantics

- `trialStartedAt`: when the hosted trial was provisioned.
- `trialExpiresAt`: the instant trial access stops. `TrialExpired` is time-driven from this timestamp.
- `readOnlyUntil`: deadline for the read-only window. While this timestamp is present and no later transition has happened, the tenant is in `ReadOnly`.
- `lockedAt`: when writes were fully locked.
- `retentionUntil`: deadline after which purge is allowed. While this timestamp is present and `purgedAt` is absent, the tenant is in `RetentionPeriod`.
- `purgedAt`: when tenant data was purged. This is terminal.

The later phases are explicit control-plane transitions; they are not inferred silently from elapsed wall-clock time.

## Public API shapes

The implemented public contract now includes:

- `TenantLifecycleQuery`
- `TenantLifecycleStatus`
- `TenantLifecycleTransitionRequest`
- `TenantLifecycleTransitionResult`
- `TenantLifecycleTimeline`
- `TenantLifecycleStateMachine`

Schemas live in `schemas/v1/`. Representative sample documents live in `conformance/samples/`.

## Relationship to entitlements

Lifecycle and entitlement remain separate concerns:

- lifecycle says what operational state the tenant is in
- entitlements say what capabilities the tenant currently holds

The entitlement bundle resolver consumes lifecycle as policy input, but the canonical hosted lifecycle model now lives here rather than being implied by entitlement-only enums.
