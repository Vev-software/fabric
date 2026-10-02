# Fabric Data-Sharing Enrollment

A customer can let a product share a **minimized summary** of its data with another product the customer has
consented to. When the source runs inside a customer network we cannot assume inbound access, so the summary is
**pushed outbound** and the consumer never pulls. The consumer must know exactly which tenant and which
consumer-side account each push belongs to, and the customer must be able to see and revoke the sharing at any
time.

This is the same problem discovery enrollment solves for scanners ([discovery enrollment](./discovery-enrollment.md)),
so it reuses that pattern instead of a product-local mechanism. Fabric owns **who may push, for which tenant, and the
audit trail**. The payload stays owned by its own contract (for Atlas, the landscape digest in `atlas-contracts`).

## What an enrollment binds

`DataSharingBinding` ties **one source tenant to one consumer-side account**. The consumer decides the binding when
it creates the enrollment. A push never chooses it: a push that names another tenant or account is denied
(`sharing_binding_mismatch`), whatever its payload says. The binding cannot be moved; a different tenant or account
needs a different enrollment.

## Lifecycle

`Pending` -> `Active` -> `Suspended` -> `Revoked`, with `Expired` as an evaluation result when the credential's
`credentialExpiresAt` has passed. As for discovery, `Expired` is not a recovery state, and there is no resume
transition. Transitions: `Activate`, `RotateCredential`, `Suspend`, `Revoke`.

- **Revocation can start from either side.** A `Revoke` names who acts (`requestedBy`: `Source` or `Consumer`), and the
  timeline records it (`revokedBy`). It takes effect at once: the very next push is denied. It works from any state
  except an enrollment that is already revoked, and the first revocation stands.
- `Activate` redeems the activation code (below) as part of the transition, so an activation cannot be applied without
  having used the code. `RotateCredential` and `Suspend` need an `Active` enrollment.

## The one-time activation code

The consumer's admin is shown a code **once**; it is entered on the source side to activate the enrollment.
`DataSharingActivationCodes` issues and redeems it:

- 120 bits of randomness, written as six groups of four characters (Crockford base32: no `I`, `L`, `O` or `U`; case,
  dashes and spaces do not matter, and look-alike letters are forgiven).
- Stored only as a **SHA-256 hash bound to the enrollment id** (`DataSharingActivationRecord`), never the code itself,
  so a code issued for one enrollment can never activate another. Hashes are compared in constant time.
- **Single use**: a successful redemption stamps `consumedAt` on the record the caller must store. A second try
  returns `sharing_activation_code_used`.
- **Expires**: at most seven days, normally minutes (`sharing_activation_code_expired`).
- A wrong code is always `sharing_activation_code_invalid`, whatever state the record is in, so a guess learns nothing.
  Limiting guesses is the service's job: it should suspend or revoke an enrollment after repeated invalid codes.

## Canonical access decision

`LocalDataSharingPushAccessEvaluator` decides whether a push may be accepted **right now**, for the capability
`atlas.landscape.share`, composing four seams in one fixed order. Deny precedence is canonical and fail-static:

1. tenant lifecycle (`lifecycle_*`),
2. enrollment state (`sharing_enrollment_revoked`, `_suspended`, `sharing_credential_expired`, `_pending`),
3. the binding (`sharing_binding_mismatch`) against what the push claims about itself (`DataSharingClaim`),
4. entitlement (`entitlement_denied`, `entitlement_snapshot_stale`, ...).

An allowed decision is valid until the earlier of the credential's expiry and the entitlement's.

## Capability

`atlas.landscape.share` is a **reserved** capability in the taxonomy: the contract and the enrollment work regardless,
and whether outbound push is part of a free edition is the edition's decision, not Fabric's.

## Lifecycle events and audit vocabulary

`DataSharingLifecycleEvent` records `EnrollmentCreated`, `EnrollmentActivated`, `CredentialRotated`, `PushDenied`,
`EnrollmentSuspended`, `EnrollmentRevoked` and `CredentialExpired`, for orchestration and timelines. Durable audit
flows through the Fabric `AuditEvent` envelope using the shared values in `DataSharingAuditVocabulary`:

- `fabric.sharing.enrollment.create`, `fabric.sharing.enrollment.activate`, `fabric.sharing.credential.rotate`,
  `fabric.sharing.enrollment.suspend`, `fabric.sharing.enrollment.revoke`
- `atlas.landscape.share.push.accept`, `atlas.landscape.share.push.deny`

## Contract surface

.NET (`Vev.Fabric.Contracts.Sharing`) and TypeScript mirrors, with schemas in `schemas/v1/` and conformance samples in
`conformance/samples/`: `data-sharing-binding`, `-enrollment-timeline`, `-activation-record`, `-enrollment-query`,
`-enrollment-status`, `-enrollment-transition-request`, `-enrollment-transition-result`, `-lifecycle-event`,
`-push-access-request` and `-push-access-decision`. The activation sample carries a real code and its hash, so the
documented flow can be replayed.

Fabric does not depend on Atlas or on any consuming product; the dependency-direction check stays green.
