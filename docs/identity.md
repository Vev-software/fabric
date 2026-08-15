# Fabric Identity — tenant & principal context

The tenant and principal context contract (`fabric#3`). Tenancy and identity are **Fabric
concerns from day one**, because isolation is a security concern (`05 §5`): a product must
never invent its own tenancy or identity model (`15 §2`, `11 §4`). A product resolves
"which tenant / which principal" from this contract **alone** — it never reaches into an
identity server or into Fabric internals (`AGENTS.md §1.2`).

## The contract

| Type | Schema | SDK |
|---|---|---|
| `TenantContext` | [`tenant-context.schema.json`](../schemas/v1/tenant-context.schema.json) | `TenantContext` (.NET / TS) |
| `PrincipalContext` | [`principal-context.schema.json`](../schemas/v1/principal-context.schema.json) | `PrincipalContext` (.NET / TS) |

### TenantContext

```jsonc
{ "tenantId": "tenant-a" }
```

`tenantId` is a stable identifier and is itself the **isolation boundary**: every asset is
isolated by it, and every request carries it. There is no separate "boundary" field — the
id is the boundary.

### PrincipalContext

```jsonc
{
  "principalId": "principal-1",        // stable OIDC 'sub', never an email or credential
  "displayName": "Atlas User",         // for audit + UX
  "roles": ["AtlasArchitect"],         // coarse roles held in the current tenant
  "claims": {                          // optional, provider-neutral, string-valued
    "iss": "https://id.example.com/",
    "preferred_username": "auser"
  }
}
```

`claims` carries selected, provider-neutral claims so a product can read identity
attributes without binding to a specific IdP's SDK. It is **never** a place for secrets,
access tokens or refresh tokens.

## Provider-neutral by design

Fabric **adopts** identity, it does not build it (`05 §3`, `05 §6`): OIDC/OAuth for
authentication and SCIM for provisioning, consumed through adapters. A bundled Keycloak is
for **local development only** — it is not an identity server we ship or operate. The
contract here is deliberately provider-neutral: nothing in `TenantContext` or
`PrincipalContext` names a provider.

## Compatibility

- The contract is **v1** (`CONTRACT_VERSION = "1"`, schemas under `schemas/v1/`).
- **Additive changes are non-breaking**: `claims` was added to `PrincipalContext` this way
  (optional; older payloads without it stay valid, and the .NET record defaults it to none).
- **Breaking changes** — removing/renaming a field or tightening a constraint — require a
  new major plus an ADR, a migration path and a deprecation period (`05 §7`, see the
  `architecture` repository).

The entitlement contracts reuse these types: `entitlement-request.schema.json` references
`tenant-context.schema.json` and `principal-context.schema.json` rather than re-declaring
them, so the identity shape has a single source of truth.
