# Fabric Discovery Enrollment

This document records the public discovery enrollment surface implemented for `atlas-enterprise#9`
on August 15, 2026.

## Scope

Fabric owns:

- tenant-bound machine enrollment for Atlas discovery scanners
- credential activation, rotation, suspension and revocation lifecycle
- lifecycle events around enrollment and denied ingestion attempts
- the shared discovery audit vocabulary emitted through the Fabric audit envelope

Fabric does **not** own scanner observation payloads, reconciliation logic, or Atlas catalogue
mutation.

## Relationship to entitlements

Discovery bootstrap and discovery ingestion stay separate:

- enrollment says *which machine principal exists for which tenant*
- entitlement says *whether that tenant currently holds `atlas.discovery.ingestion`*

The public capability remains `atlas.discovery.ingestion`. Enrollment is not a second plan switch
or a product-local bypass around entitlements. If the enrollment is not `Active`, or the credential
is expired, the runtime denies rather than downgrading. That is the fail-static rule for discovery.

## Enrollment lifecycle

The canonical discovery enrollment lifecycle is:

`Pending` -> `Active` -> `Suspended` -> `Revoked`

`Expired` is an evaluation result when `credentialExpiresAt` has passed; it is not a recovery state.
Recovery happens by activating or rotating a valid credential before use.

Supported transitions:

- `Activate`
- `RotateCredential`
- `Suspend`
- `Revoke`

There is intentionally no resume transition. A suspended or revoked enrollment must be handled by
an explicit control-plane decision rather than local best-effort recovery.

## Lifecycle events

The public `DiscoveryLifecycleEvent` contract records:

- `EnrollmentCreated`
- `EnrollmentActivated`
- `CredentialRotated`
- `AccessDenied`
- `EnrollmentSuspended`
- `EnrollmentRevoked`
- `CredentialExpired`

These events are for orchestration, status stitching and operational timelines. Durable audit still
flows through the Fabric `AuditEvent` envelope.

## Canonical access decision

Fabric now also owns the canonical decision for whether a tenant-scoped machine principal may
perform `atlas.discovery.ingestion` right now.

`LocalDiscoveryIngestionAccessEvaluator` composes three public seams in one fixed order:

1. tenant lifecycle
2. discovery enrollment state
3. entitlement decision

Deny precedence is canonical and fail-static:

- tenant lifecycle denies first (`DataPurged`, `RetentionPeriod`, `Locked`, `ReadOnly`, `TrialExpired`)
- enrollment denies second (`Revoked`, `Suspended`, `Expired`, `Pending`)
- entitlement denies third (for example `entitlement_denied`, `entitlement_snapshot_stale`)

This keeps Atlas from hand-rolling policy composition or reason precedence locally.

## Audit vocabulary

Products emit discovery audit records through the existing Fabric audit envelope using the shared
action and resource values in `DiscoveryAuditVocabulary`, including:

- `fabric.discovery.enrollment.create`
- `fabric.discovery.enrollment.activate`
- `fabric.discovery.credential.rotate`
- `fabric.discovery.enrollment.suspend`
- `fabric.discovery.enrollment.revoke`
- `atlas.discovery.ingestion.accept`
- `atlas.discovery.ingestion.deny`

This keeps the audit shape Fabric-owned while making discovery action values explicit and stable.
