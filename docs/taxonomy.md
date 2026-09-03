# Fabric Taxonomy

This document records the current taxonomy and reason-code rules implemented for `fabric#7`.

## Naming rules

Capability ids and limit keys are:

- lowercase
- dot-separated namespaces
- allowed characters: `a-z`, `0-9`, `.`, `-`
- stable once published

Examples:

- `atlas.catalogue.read`
- `atlas.analysis.integration-map`
- `atlas.users`
- `fabric.marketplace.install`
- `portic.governance.policy.advanced`

Products may add ids in their namespace, but existing ids do not change meaning.

Resource ids follow the same lowercase/stable rule, but allow path-like scoping:

- allowed characters: `a-z`, `0-9`, `.`, `-`, `:`, `/`
- example: `atlas:asset/app-checkout`
- example: `portic:provider/openai-primary`

## Seeded Atlas taxonomy

Current Atlas feature ids seeded in Fabric:

- `atlas.catalogue.read`
- `atlas.catalogue.write`
- `atlas.export.portable-bundle`
- `atlas.analysis.integration-map`
- `atlas.analysis.eol`
- `atlas.analysis.apm`
- `atlas.analysis.roadmap`
- `atlas.ai.review`
- `atlas.ai.generate`
- `atlas.discovery.ingestion`
- `atlas.data.introspection`
- `atlas.data.overlap`
- `atlas.data.quality`
- `atlas.portal.readonly`
- `atlas.export.archimate`

Current Atlas limit keys:

- `atlas.entities`
- `atlas.users`
- `atlas.storage`
- `atlas.workspaces`
- `atlas.import.jobs`
- `atlas.repository.application.max`

Reserved paid Atlas capabilities are marked as reserved in the catalog so downstream module or
entitlement work can treat them explicitly as commercial seams. This is the single source of truth
for the reserved set: downstream editions (for example the Atlas Community `ReservedPaid` set) key
their entitlement gates on exactly these ids and must not invent parallel strings. The reserved
commercial seams are:

- `atlas.analysis.integration-map`
- `atlas.analysis.eol`
- `atlas.analysis.apm`
- `atlas.analysis.roadmap`
- `atlas.ai.review`
- `atlas.ai.generate`
- `atlas.discovery.ingestion`
- `atlas.data.introspection`
- `atlas.data.overlap`
- `atlas.data.quality`
- `atlas.export.archimate`

`atlas.export.archimate` resolves from Hosted Trial and every Starter-or-higher offer. Lifecycle
restriction still removes it in read-only and export-only states, so the normal portability escape
hatch remains the portable bundle rather than an EA export surface.

## Shared decision reasons

The current shared reason-code catalog covers:

- generic allow/role reasons
- entitlement grant/deny/unavailable reasons
- signed snapshot validation/staleness reasons
- lifecycle deny reasons for trial-expired, read-only, locked, retention and purged states

These reasons are machine-readable and stable. Products render them, but do not invent their own equivalents for the same policy outcomes.

## Public contract shape

The public taxonomy contract now exists in three places:

- .NET definitions in `src/Vev.Fabric.Contracts/Taxonomy`
- TypeScript mirror in `sdk/typescript/src/index.ts`
- schema and sample document in `schemas/v1/taxonomy-catalog.schema.json` and `conformance/samples/taxonomy-catalog.sample.json`
