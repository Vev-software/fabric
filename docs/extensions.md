# Fabric Extensions — the module foundation

The extension model foundation (`fabric#10`, handbook `07-Module-System`, `08-Marketplace`).
Fabric owns the extension *contract* — the closed set of extension types, the manifest, and the
one hard guard that keeps the module system from becoming a back-door around the paid edition.
Fabric depends on no product (`AGENTS.md §1.1`); products contribute their own capability ids.

## A closed set of extension types

There is **no universal plug-in host** (`AGENTS.md §1.7`). An extension is one of a known,
closed set (`07 §2`):

`provider-adapter` · `importer-exporter` · `policy-pack` · `workflow-action` · `connector` ·
`ui-extension` · `domain-module`

## The manifest — deny by default

An extension ships an [`extension-manifest.schema.json`](../schemas/v1/extension-manifest.schema.json)
(`ExtensionManifest`). It declares what the extension is and — **deny by default** — exactly what
it may touch. Nothing is granted unless the manifest declares it.

```jsonc
{
  "id": "com.acme.archimate-importer",
  "version": "1.2.0",
  "publisher": "Acme Corp",
  "type": "importer-exporter",
  "compatibleWith": { "fabricApi": "^1.0", "product": ">=2.0 <3.0" },
  "permissions": [ { "value": "atlas.catalogue.read" }, { "value": "atlas.catalogue.write" } ],
  "resources": [ { "value": "atlas:catalogue/main" } ],
  "network": ["api.acme.example.com"],
  "secrets": ["acme-api-key"]
}
```

`compatibleWith.fabricApi` is a required SemVer range; `product` is present only for
product-specific extensions. `permissions`, `resources`, `network` and `secrets` are all optional
and default to nothing — an absent section grants nothing.

## The reserved-capability guard (the anti-back-door)

The single guard that matters: **a manifest may never declare — or satisfy at runtime — a reserved
paid capability** (`09 §3`). Capability ids owned by Fabric are marked `reserved` in the taxonomy
(`fabric#7`, `CapabilityDefinition.Reserved`). A module extends the edges; it can never flip a
reserved capability to allowed. Whether a tenant may use a paid capability is the **entitlement
decision's** job, not the module's.

`ExtensionManifestValidator.Validate` enforces it, returning a machine-readable reason:

```csharp
var result = ExtensionManifestValidator.Validate(manifest);
// result.Valid == false, error.Code == "extension_reserved_capability"
// when the manifest declares e.g. atlas.ai.review
```

Reason codes: `extension_missing_id`, `extension_missing_version`, `extension_missing_publisher`,
`extension_missing_fabric_api_compatibility`, `extension_reserved_capability`.

## Install is entitlement-checked

Installing an extension is not a policy `if (plan == …)`. The install lifecycle raises
`fabric.marketplace.install` as an **entitlement** request, so the commercial control plane governs
who installs what through the normal entitlement decision (`07 §6`):

```csharp
var request = ExtensionInstall.InstallEntitlementRequest(manifest, tenant, principal);
// request.Capability == fabric.marketplace.install, request.Resource == the extension id
```

## Boundaries

- The first community-capable runtime is **out-of-process**, not in-process assemblies (`07 §4`).
- Fabric holds the capability taxonomy; product-specific ids are contributed by products
  (`atlas-contracts`, `portic-sdk`), never by Fabric reaching into a product.
- Conformance vectors for third-party validators are handed to `fabric-conformance` (separate issue).

## Compatibility

- The contract is **v1** (schema under `schemas/v1/`).
- Additive changes (a new optional manifest field, a new reason code) are non-breaking. Adding an
  extension **type** is additive for producers but a consumer that must handle every type treats it
  as a minor that needs handling.
- Breaking changes require a new major plus an ADR, a migration path and a deprecation period
  (`05 §7`). See the `architecture` repository.
