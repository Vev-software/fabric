# fabric

Shared contracts, schemas and SDKs used across VEV products.

`fabric` is the public contract layer of VEV's internal substrate. It defines the
cross-cutting concerns — tenant context, entitlements, audit, events, observability
conventions, the extension manifest and the AI contract — as versioned contracts with
.NET and TypeScript SDKs. Products depend on these packages; third parties validate
against the schemas.

Fabric is not a product. It is not sold, has no price and no standalone roadmap. It
exists so that the products built on it stay consistent in exactly the places where
inconsistency would be a security or operational risk.

## What lives here

- **Foundation contracts** (no product-domain concepts): tenant and principal context,
  capability and resource identifiers, entitlement request/decision with reason codes,
  authorizer interface, audit event, extension manifest (JSON Schema), event envelope
  and naming conventions, the AI chat/routing/tool contracts, and the error model.
- **SDKs**: .NET and TypeScript packages for the contracts above.
- **Schemas**: JSON Schema / OpenAPI definitions, versioned.
- **Local runtime**: a reference implementation for development and testing.

## What does not live here

- **No product domain.** No enterprise-architecture concepts, no gateway routing logic,
  no application-specific behaviour. If it knows what a product's domain object is, it is
  not Fabric.
- **No AI implementation.** Fabric defines the AI contract only. Routing, provider
  adapters and prompt libraries live in Portic. Fabric never depends on a product.
- **No billing or payments.** Fabric owns the *entitlement* model; commercial billing
  sits behind an adapter, elsewhere.
- **No identity server.** Adopt OIDC/SCIM providers; Fabric ships adapters.

## Versioning and compatibility

Public contracts and packages follow Semantic Versioning. Breaking a public contract
requires an ADR, a migration path, a deprecation period and compatibility tests. See the
`architecture` repository for the decision records that govern this repo.

## Contributing

Contributions are welcome under the terms in `CONTRIBUTING.md`. Because VEV operates an
open-core model, contributions to this repository require a sign-off (DCO or CLA as stated
per repo).

## Current entitlement surface

The first implemented Fabric slice is the entitlement contract and local evaluator for `fabric#4`:

- .NET contracts and evaluator in [`src/Vev.Fabric.Contracts`](./src/Vev.Fabric.Contracts)
- JSON Schemas in [`schemas/v1`](./schemas/v1)
- TypeScript SDK mirror in [`sdk/typescript`](./sdk/typescript)
- conformance samples in [`conformance/samples`](./conformance/samples)
- design/runtime notes in [`docs/entitlements.md`](./docs/entitlements.md)

This surface is intentionally control-plane independent on the request path: products evaluate
the last accepted signed snapshot locally and fail static when it goes stale beyond grace.

## License

Apache-2.0. The contract layer is permissive by design: its value is broad adoption.

---

*VEV — Engineering clarity.*
