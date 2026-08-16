# Entitlement evaluator hardening

The offline evaluator (`fabric#4`) is deliberately **offline-capable and fail-static** so a
control-plane outage never stops authorised production. On offline / air-gapped / self-hosted hosts
that same property is the weak point: the host and its clock are under the customer's (or an
attacker's) control. This is the Fabric-side (contract + reference evaluator) hardening for the
licensing threat model (`security#1`, threats T3/T4/T7). The signer and keys stay in
`fabric-control-plane`; Fabric owns the **semantics** only.

## Threat → control

| Threat | Attack | Control (in `LocalEntitlementEvaluator`) | Reason code |
|---|---|---|---|
| **T3 rollback/replay** | Re-present an older, more generous snapshot | **Anti-rollback watermark**: refuse any snapshot older than the highest already seen — by `counter` when present, else `issuedAt`. Equal is accepted (idempotent re-fetch); downgrade only via a *newer* snapshot. | `entitlement_snapshot_rolled_back` |
| **T4 clock manipulation** | Roll the wall clock back so an expired snapshot/trial reads as valid | **Forward-only observed time**: a verified snapshot raises the floor to its `issuedAt`; a request whose clock reads earlier than the last observed time is denied, not trusted. Grace is measured forward-only. | `entitlement_clock_regression` |
| **T7 trial fail-static-open** | Block the control plane to freeze a trial open indefinitely | **Trial hard-stop**: a `trial` grant is denied the moment it passes `expiresAt`, with **no grace**. The "never stop authorised production" guarantee applies to *purchased* sources only. | `trial_expired` |

`verify-before-use` remains: an unknown signing key, a bad signature, or a malformed payload denies
with `entitlement_snapshot_invalid` — never grants (R1).

## The asymmetry (why purchased ≠ trial)

- **Purchased** (`subscription`, …): fails static. After `expiresAt` it **freezes open** until
  `graceUntil`, so a control-plane outage never stops paid, authorised production.
- **Trial**: hard-stops. After `expiresAt` it **denies** (`trial_expired`); an outage can never keep
  a trial alive. Trials carry a hard `expiresAt` with no outage-driven grace extension.

## Trusted time

The evaluator trusts, in order: the signed `issuedAt` as a **lower bound** (a valid snapshot proves
time was at least its issuance); forward-only observed time (never moves backwards); and, in
connected mode, an optional trusted-time/heartbeat token from the control plane. **Residual risk:**
a fully air-gapped host with no newer snapshot and a forward-set clock can still advance time within
a snapshot's own validity — bounded by `expiresAt`/`graceUntil` and by the next snapshot's watermark.
Shorten grace and snapshot lifetimes to shrink that window.

## Compatibility

`counter` is an **optional additive** field on `EntitlementSnapshot`; snapshots without it fall back
to `issuedAt`-based anti-rollback. The new reason codes are additive. Purchased fail-static behaviour
is unchanged. See `05 §7` and the `architecture` repository for the breaking-change bar.
