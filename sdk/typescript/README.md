# @vev-software/fabric-contracts

Public, versioned **cross-cutting contracts** for the VEV substrate — the tenant/principal,
entitlement, taxonomy and audit concerns that every VEV product shares, defined once so the
places where inconsistency would be a security or operational risk stay consistent.

```sh
npm i @vev-software/fabric-contracts
```

```ts
import type { PrincipalContext, EntitlementDecision } from "@vev-software/fabric-contracts";
```

## What's inside

- **Tenant & principal context** — who is acting, in which tenant.
- **Entitlements** — entitlement request/decision with reason codes, the authorizer interface,
  and signed entitlement snapshots.
- **Tenant lifecycle** and the **shared taxonomy**.
- **Append-only audit event envelope** — the canonical shape products emit to the audit trail.
- **Authoritative JSON Schemas** — shipped under `schemas/v1/` in the package, so you can validate
  payloads against the exact same contracts the TypeScript types describe.

Fabric is not a product: this package is the **contract layer** only, with no product-domain
concepts and no implementation. Ships type declarations (`dist/index.d.ts`) and ESM; no runtime
dependencies.

## Versioning

[SemVer](https://semver.org), derived from the git tag (`vX.Y.Z`) at release. Pre-1.0, the v1
contract evolves additively across minor/patch versions.

## Links

- **Source & issues:** https://github.com/Vev-software/fabric
- **.NET SDK (NuGet):** [`Vev.Fabric.Contracts`](https://www.nuget.org/packages/Vev.Fabric.Contracts)

## Licence

[Apache-2.0](https://github.com/Vev-software/fabric/blob/main/LICENSE).
