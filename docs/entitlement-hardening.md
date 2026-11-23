# Entitlement evaluator behaviour

The offline evaluator (`fabric#4`) is deliberately **offline-capable and fail-static**: a control-plane
outage never stops authorised production. This page documents the contract-level semantics of the
reference evaluator (`LocalEntitlementEvaluator`) and its reason codes. Signing, keys and any
deployment-specific hardening are not part of Fabric.

## Reason codes

| Situation | Behaviour | Reason code |
|---|---|---|
| An older snapshot than the highest already seen | Refused. Ordering uses `counter` when present, else `issuedAt`. Equal is accepted (idempotent re-fetch). | `entitlement_snapshot_rolled_back` |
| A request clock earlier than the last observed time | Denied, not trusted. Time only moves forward. | `entitlement_clock_regression` |
| A `trial` grant past `expiresAt` | Denied, with no grace. | `trial_expired` |
| Unknown signing key, bad signature or malformed payload | Denied, never granted. | `entitlement_snapshot_invalid` |

`verify-before-use` always applies: a snapshot is verified before any grant is honoured.

## Purchased versus trial

- **Purchased** sources (`subscription`, …) fail static: after `expiresAt` they remain valid until
  `graceUntil`, so an outage does not stop paid, authorised production.
- **Trial** grants hard-stop at `expiresAt` and carry no outage-driven grace.

## Compatibility

`counter` is an **optional, additive** field on `EntitlementSnapshot`; snapshots without it fall back
to `issuedAt`-based ordering. The reason codes are additive. Purchased fail-static behaviour is
unchanged.
