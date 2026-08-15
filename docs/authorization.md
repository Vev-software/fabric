# Fabric Authorization

This document records the current public Fabric authorization surface implemented for `fabric#5`.

## Scope

Fabric owns:

- the authorization mechanism: may principal `P` perform action `A` on resource `R`
- the typed `IAuthorizer` abstraction and shared allow/deny reason codes
- shared `ResourceId`, tenant and principal context usage
- a simple local reference implementation for development and tests

Fabric does **not** own a product's role names, permission catalog or policy engine choice.

## Boundary

Products define their own action names and role names. Fabric evaluates them.

That keeps the split explicit:

- authorization asks whether a principal may perform an action on a resource
- entitlements ask whether a tenant holds a purchased capability

Those concerns stay separate even when a product consults both on the same request path.

## Product registration model

A product registers coarse role requirements through `AuthorizationPolicyRegistry`:

```csharp
var policies = new AuthorizationPolicyRegistry()
    .Require("atlas.catalogue.write", "AtlasArchitect")
    .Require("atlas.export.portable-bundle", "AtlasArchitect", "AtlasCustomer");

IAuthorizer authorizer = new LocalAuthorizer(policies);
```

The product supplies names and mappings; it does not own the decision mechanism.

## Adapter model

`IAuthorizer` is the stable seam. A local implementation is included for dev/test use, while a
real deployment can adapt the same contract to an external PDP such as OPA or OpenFGA.

## Public API shapes

The implemented public contract now includes:

- `AuthorizationRequest`
- `AuthorizationDecision`
- `IAuthorizer`
- `AuthorizationPolicyRegistry`
- `LocalAuthorizer`

Schemas live in `schemas/v1/`. Representative sample documents live in `conformance/samples/`.
