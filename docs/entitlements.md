# Fabric Entitlements

This document records the current public Fabric entitlement surface implemented for `fabric#4`.

## Scope

Fabric owns:

- entitlement request and decision contracts
- signed snapshot documents for connected and air-gapped distribution
- local evaluation on the request path
- declarative bundle translation from commercial offer + lifecycle state into grants and limits

Fabric does **not** own billing adapters, payments or vendor checkout logic.

## Runtime model

The hot path stays local:

1. A control-plane or offline operator provides a signed entitlement snapshot.
2. The local runtime verifies the signature and parses the document.
3. The evaluator answers entitlement checks from the last accepted snapshot.
4. If the snapshot is stale beyond grace, the evaluator denies rather than granting.

That is the fail-static rule: outages do not silently broaden access.

## Bundle translation

Products do not branch on `plan == ...`. Instead, Fabric translates a declarative request:

- offer: `CommunitySelfHosted`, `HostedTrial`, `HostedStarter`, `Pro`, `Enterprise`, `SelfHostedEnterprise`
- lifecycle input: `Active`, `TrialActive`, `TrialExpired`, `ReadOnly`, `Locked`, `RetentionPeriod`, `DataPurged`

into explicit capability grants and limit keys. The resulting grants are what get packed into the signed snapshot.

Current lifecycle policy:

- `TrialActive` and `Active` keep the base offer grants.
- `TrialExpired` and `ReadOnly` reduce the tenant to read-only/export surfaces.
- `Locked` and `RetentionPeriod` reduce the tenant to export-only.
- `DataPurged` grants nothing.

The canonical lifecycle contract is tracked separately in `fabric#8`; this document describes the entitlement-side policy input already consumed by bundle translation.

## Public API shapes

The implemented public contract now includes:

- single and batch entitlement evaluation payloads
- signed snapshot import payloads
- snapshot, grant and decision documents

Schemas live in `schemas/v1/`. The TypeScript SDK mirrors the same contract in `sdk/typescript/`.

The shared `TenantContext`, `PrincipalContext` and `ResourceId` primitives are also consumed by the
authorization surface described in `docs/authorization.md`; authorization and entitlement stay distinct
even when a product evaluates both on one request.

## Conformance fixtures

Representative sample documents live in `conformance/samples/` for:

- batch evaluate request
- signed snapshot document
- signed snapshot import request

The test suite loads these fixtures to prove the public documents stay deserializable as the contract evolves.
