# ADR 0001: Tenant membership snapshot contract

Status: proposed. Tracks `fabric#57`.

## Context

Fabric's authorization contracts describe a decision over a tenant and a principal, and
`PrincipalContext` carries the roles a principal holds *in the current request*. They give a
product no portable way to say "these identities are the members of this tenant". Products that
need to apply membership changes from an authoritative source would each invent their own shape,
version rules and ordering rules, and would disagree on edge cases (identity scope, removal,
replay, expiry).

## Decision

Add an additive, versioned `TenantMembershipSnapshot` to `Vev.Fabric.Contracts` and
`@vev-software/fabric-contracts`, with an authoritative JSON Schema (`schemas/v1`).

**Ownership.** Fabric owns the shape and the apply semantics. It does not own transport,
authentication of a snapshot, authentication of a request, credentials, or the meaning of a
role. Products consume the contract; Fabric references no product (dependency direction is
unchanged and enforced by the existing fitness checks).

**Identity.** A member is `(issuer, subject)`, compared exactly. A bare subject is never an
identity. This is explicit so that two issuers can never alias each other.

**Completeness.** A snapshot is the complete membership; absence means "not a member". There is
no disabled flag and no partial or delta form in v1. Removal is a newer snapshot.

**Ordering.** `sequence` is monotonic per tenant. A lower value is rejected as rollback; an equal
value is accepted only for identical content; anything else at the same value is invalid.

**Validity.** `validUntil` is mandatory. Membership ends at that instant without further
messages, so a consumer cut off from its source fails closed.

**Roles.** Role names are opaque strings, at least one per member. They are data, not policy.

**Reference consumer.** `TenantMembershipRegistry` implements the rules and the stable reason
codes. Products may reimplement them; the documented semantics are the contract.

## Version semantics

- `schemaVersion` is an integer; this ADR defines `1`.
- A consumer rejects every version it does not know. It never guesses or downgrades.
- Compatible additions (new optional fields) keep `1` only if an older consumer can ignore them
  without changing a decision. The v1 schema sets `additionalProperties: false`, so such an
  addition is published as a new schema version rather than slipped into `1`.
- Anything that changes the meaning of an existing field, or the rules above, is a new
  `schemaVersion` and needs its own ADR, a migration path and compatibility tests.
- The package ships through the normal tag-driven release; no version number is edited by hand.

## Compatibility

Purely additive: new types, one schema, new reason codes (`membership_*`) and taxonomy entries.
No existing contract, schema or reason code changes. `PrincipalContext` and the authorization
contracts are untouched; a product maps a resolved member to its own roles and then uses the
existing authorization surface.

## Consequences

- Consumers share one definition of identity scope, removal, replay and expiry.
- Authenticating or delivering a snapshot stays a separate, product-chosen concern, so this
  contract works with any trust model or transport without a private dependency.
- Delta/partial updates, per-member expiry and groups are deliberately out of v1 and would be
  new versions.
- Persistence of the applied `sequence` is the consumer's responsibility; the reference registry
  is in memory.

## Evidence

Conformance fixtures cover a supported shape, a second tenant, and invalid shapes (unknown
version, missing issuer, empty and duplicate roles). Tests cover version, tenant and identity
scope, removal, rollback, idempotent redelivery, expiry and clock regression.
