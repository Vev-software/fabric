# Fabric Customer Enrolment

This document describes the public `CustomerEnrolmentSnapshot` contract (`fabric#69`): a
product-neutral way to say "this customer is enrolled in products A and B".

## Scope

Fabric owns:

- an opaque `CustomerRef` that every product can use to refer to the same customer
- the portable shape of a customer's product enrolments (product, offer, source, status, period)
- the version and ordering rules a consumer applies

Fabric does **not** own:

- any personal data (e-mail, name, address); these stay with whoever issues the snapshot
- prices, plans, discounts or payment concepts (Fabric owns entitlements, not billing)
- what an offer id means, or what an enrolment unlocks inside a product
- how a snapshot is authenticated, signed or delivered

A snapshot is data. A consumer decides separately that it came from a source it trusts.

## Shape

Schema: [`customer-enrolment-snapshot.schema.json`](../schemas/v1/customer-enrolment-snapshot.schema.json).
Sample: [`customer-enrolment-snapshot.sample.json`](../conformance/samples/customer-enrolment-snapshot.sample.json).

| Field | Meaning |
|---|---|
| `schemaVersion` | Contract version. Only `1` exists; consumers reject any other value. |
| `customer.customerId` | Opaque, stable identifier. Compared exactly; never an e-mail or a name. |
| `sequence` | Monotonic revision for the customer, `>= 1`. A consumer never accepts a lower one. |
| `issuedAt` | When the snapshot was produced. |
| `enrolments[].productId` | Lower-case product identifier (`^[a-z][a-z0-9-]*$`). At most one enrolment per product. |
| `enrolments[].offerId` | Opaque issuer-chosen offer identifier; no price or tier meaning. |
| `enrolments[].source` | `Paid`, `Gift`, `Trial` or `Bundle`. |
| `enrolments[].status` | `Active`, `Suspended` or `Ended`. |
| `enrolments[].from` / `until` | Enrolment period; `until` absent or `null` means open-ended, otherwise it must be later than `from`. |

The list is **complete**: absence of a product means "not enrolled".

The schema rejects unknown properties, so personal data or commercial fields cannot be added
without a new schema version. Cross-field rules JSON Schema cannot express (one enrolment per
product, `until` later than `from`) are the issuer's responsibility; a consumer should treat a
violation as an invalid snapshot.

## Relation to other contracts

- `TenantMembershipSnapshot` (`fabric#57`) answers who belongs to a tenant. This contract answers
  which products a customer is enrolled in; it does not replace tenant context or entitlements.
- Entitlements remain the authority for what is allowed inside a product.

## Consuming it

.NET: `CustomerEnrolmentSnapshot`, `CustomerRef` and `ProductEnrolment` in
`Vev.Fabric.Contracts.Customers`. TypeScript: the same-named types in `@vev-software/fabric-contracts`.
Negative fixtures live in [`conformance/invalid`](../conformance/invalid); each must be rejected by its schema.
