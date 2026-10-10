# Fabric Tenant Membership

This document describes the public `TenantMembershipSnapshot` contract (`fabric#57`) and how a
consumer applies it. The decision record is [ADR 0001](./adr/0001-tenant-membership-snapshot.md).

## Scope

Fabric owns:

- the portable shape of "who is a member of this tenant, with which role names"
- the version, ordering and validity rules a consumer applies
- a reference consumer, `TenantMembershipRegistry`, and stable reason codes

Fabric does **not** own:

- how a snapshot is authenticated, signed or delivered (transport and trust anchors)
- authentication of a request (OIDC token validation stays with the product)
- what a role name allows (product role policy)
- credentials of any kind

A snapshot is data. A consumer decides separately that the data came from a source it trusts.

## Shape

Schema: [`tenant-membership-snapshot.schema.json`](../schemas/v1/tenant-membership-snapshot.schema.json).
Sample: [`tenant-membership-snapshot.sample.json`](../conformance/samples/tenant-membership-snapshot.sample.json).

| Field | Meaning |
|---|---|
| `schemaVersion` | Contract version. Only `1` exists. |
| `tenant.tenantId` | The tenant the membership is scoped to. |
| `sequence` | Monotonic revision within the tenant, `>= 1`. |
| `issuedAt` / `validUntil` | Validity window; `validUntil` must be later than `issuedAt`. |
| `members[].identity` | `issuer` + `subject`, the OIDC `iss` and `sub`. |
| `members[].roles` | One or more opaque, unique role names. |

The list is **complete**: absence means "not a member". There is no per-member disabled flag, so
removing someone is publishing a newer snapshot without them.

## Applying a snapshot

A consumer applies a snapshot, in this order, and fails closed on every step:

1. **Version.** Reject any `schemaVersion` it does not know (`membership_snapshot_unsupported_version`).
2. **Tenant.** Reject a snapshot whose tenant is not the one the consumer serves
   (`membership_snapshot_tenant_mismatch`).
3. **Shape.** Reject duplicate identities, empty or duplicate roles, empty issuer/subject,
   `sequence < 1` or `validUntil <= issuedAt` (`membership_snapshot_invalid`).
4. **Order.** Reject a lower `sequence` than the one applied (`membership_snapshot_rolled_back`).
   An equal `sequence` is accepted only when the content is identical (idempotent redelivery);
   different content at the same sequence is `membership_snapshot_invalid`.
5. **Validity.** Reject a snapshot that is already past `validUntil` (`membership_snapshot_expired`).

Only then does it replace the previous snapshot, atomically. A rejected snapshot never changes
state.

## Resolving an identity

Lookup is by exact `(issuer, subject)`. Both are compared ordinally; a subject is unique only
within its issuer, so the same `sub` from two issuers are two different identities.

The result is `member` plus the bound role names, or a denial:

| Reason code | When |
|---|---|
| `membership_snapshot_unavailable` | No snapshot has been applied yet. |
| `membership_snapshot_expired` | Now is at or past `validUntil`. |
| `membership_not_found` | The identity is not in the list. |
| `entitlement_clock_regression` | The clock moved backwards past the last observation. |

Nothing is a member before a snapshot is applied. Membership stops at `validUntil` without any
further message, so a consumer that stops receiving snapshots loses membership rather than
keeping it indefinitely.

## What a consumer still decides

- **Authenticity.** Verify the snapshot with whatever trust it has configured before applying it.
- **Clock-skew tolerance.** The reference registry compares against its clock exactly; a
  consumer that needs tolerance applies it before calling the registry.
- **Persistence.** Keep the applied `sequence` across restarts if rollback protection must
  survive them. The reference registry is in memory.
- **Role policy.** Map role names to what they allow. Role names are opaque to Fabric.
- **Authentication.** Membership is not authentication: a request must still carry a valid token
  from the exact `issuer` the member is bound to. Role claims in a token do not add roles.

## Consuming it

.NET:

```csharp
var registry = new TenantMembershipRegistry("tenant-a");
var applied = registry.Apply(snapshot);            // validates, then adopts
var who = registry.Resolve(issuer, subject);       // who.IsMember, who.Roles, who.ReasonCode
```

TypeScript consumers use the `TenantMembershipSnapshot` type and the same rules above; the
JSON Schema is authoritative. Negative fixtures live in
[`conformance/invalid`](../conformance/invalid); each must be rejected by its schema.
