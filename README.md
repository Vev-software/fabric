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

## Current authorization surface

The current authorization slice implemented for `fabric#5` includes:

- .NET authorization contracts and local reference implementation in [`src/Vev.Fabric.Contracts/Authorization`](./src/Vev.Fabric.Contracts/Authorization)
- JSON Schemas in [`schemas/v1/authorization-request.schema.json`](./schemas/v1/authorization-request.schema.json) and [`schemas/v1/authorization-decision.schema.json`](./schemas/v1/authorization-decision.schema.json)
- TypeScript SDK mirror in [`sdk/typescript`](./sdk/typescript)
- conformance samples in [`conformance/samples`](./conformance/samples)
- design/runtime notes in [`docs/authorization.md`](./docs/authorization.md)

Products register role requirements while Fabric owns the `IAuthorizer` mechanism, keeping
authorization separate from entitlement evaluation and external PDP choice.

## Current lifecycle surface

The current hosted lifecycle slice implemented for `fabric#8` includes:

- .NET lifecycle contracts and state machine in [`src/Vev.Fabric.Contracts/Lifecycle`](./src/Vev.Fabric.Contracts/Lifecycle)
- JSON Schemas in [`schemas/v1`](./schemas/v1)
- TypeScript SDK mirror in [`sdk/typescript`](./sdk/typescript)
- conformance samples in [`conformance/samples`](./conformance/samples)
- timestamp semantics and runtime notes in [`docs/lifecycle.md`](./docs/lifecycle.md)

This makes trial expiry, read-only, lock, retention and purge states explicit public contracts
instead of product-local flags or scheduler-specific assumptions.

## Current entitlement surface

The first implemented Fabric slice is the entitlement contract and local evaluator for `fabric#4`:

- .NET contracts and evaluator in [`src/Vev.Fabric.Contracts`](./src/Vev.Fabric.Contracts)
- JSON Schemas in [`schemas/v1`](./schemas/v1)
- TypeScript SDK mirror in [`sdk/typescript`](./sdk/typescript)
- conformance samples in [`conformance/samples`](./conformance/samples)
- design/runtime notes in [`docs/entitlements.md`](./docs/entitlements.md)

This surface is intentionally control-plane independent on the request path: products evaluate
the last accepted signed snapshot locally and fail static when it goes stale beyond grace.

## Current taxonomy surface

The next implemented Fabric slice is the public taxonomy and reason-code catalog for `fabric#7`:

- seeded capability, limit and reason-code definitions in [`src/Vev.Fabric.Contracts/Taxonomy`](./src/Vev.Fabric.Contracts/Taxonomy)
- TypeScript mirror constants and types in [`sdk/typescript`](./sdk/typescript)
- public schema in [`schemas/v1/taxonomy-catalog.schema.json`](./schemas/v1/taxonomy-catalog.schema.json)
- conformance sample in [`conformance/samples/taxonomy-catalog.sample.json`](./conformance/samples/taxonomy-catalog.sample.json)
- naming rules and seeded ids in [`docs/taxonomy.md`](./docs/taxonomy.md)

This keeps capability ids, limit keys and decision reasons explicit and stable instead of leaving
them implied by scattered string literals across products.

## License

Apache-2.0. The contract layer is permissive by design: its value is broad adoption.

---

*VEV — Engineering clarity.*
