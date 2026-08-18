# Vev.Fabric.Contracts

Public, versioned **cross-cutting contracts** for the VEV substrate — the tenant/principal,
entitlement, taxonomy and audit concerns that every VEV product shares, defined once so the
places where inconsistency would be a security or operational risk stay consistent.

```sh
dotnet add package Vev.Fabric.Contracts
```

## What's inside

- **Tenant & principal context** — who is acting, in which tenant.
- **Entitlements** — entitlement request/decision with reason codes, the authorizer interface,
  and signed entitlement snapshots.
- **Tenant lifecycle** and the **shared taxonomy**.
- **Append-only audit event envelope** — the canonical shape products emit to the audit trail.
- **Authoritative JSON Schemas** — bundled under `schemas/v1/` in the package, so any language
  can validate against the exact same contracts the SDK exposes.

Fabric is not a product: this package is the **contract layer** only, with no product-domain
concepts and no implementation. The types are plain records/enums with `System.Text.Json`
attributes and no runtime dependencies beyond the base class library.

## Versioning

[SemVer](https://semver.org), derived from the git tag (`vX.Y.Z`) at release. Pre-1.0, the v1
contract evolves additively across minor/patch versions.

## Links

- **Source & issues:** https://github.com/Vev-software/fabric
- **TypeScript SDK (npm):** [`@vev-software/fabric-contracts`](https://www.npmjs.com/package/@vev-software/fabric-contracts)

## Licence

[Apache-2.0](https://github.com/Vev-software/fabric/blob/main/LICENSE).
