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

- `atlas.history.view`
- `atlas.history.timeline`

- `atlas.catalogue.read`
- `atlas.catalogue.write`
- `atlas.export.portable-bundle`
- `atlas.analysis.integration-map`
- `atlas.analysis.eol`
- `atlas.analysis.apm`
- `atlas.analysis.roadmap`
- `atlas.ai.review`
- `atlas.ai.generate`
- `atlas.ai.structure.bulk`
- `atlas.discovery.ingestion`
- `atlas.data.introspection`
- `atlas.data.overlap`
- `atlas.data.quality`
- `atlas.portal.readonly`
- `atlas.export.archimate`

Current Atlas limit keys:

- `atlas.history.window-months`

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

- `atlas.history.timeline`

- `atlas.analysis.integration-map`
- `atlas.analysis.eol`
- `atlas.analysis.apm`
- `atlas.analysis.roadmap`
- `atlas.ai.review`
- `atlas.ai.generate`
- `atlas.ai.structure.bulk`
- `atlas.discovery.ingestion`
- `atlas.data.introspection`
- `atlas.data.overlap`
- `atlas.data.quality`
- `atlas.export.archimate`

`atlas.export.archimate` resolves from Hosted Trial and every Starter-or-higher offer. Lifecycle
restriction still removes it in read-only and export-only states, so the normal portability escape
hatch remains the portable bundle rather than an EA export surface.

## Batch document structuring

`atlas.ai.structure.bulk` is a reserved feature for batch document structuring into a
reviewable landscape draft. It is exposed as `AtlasTaxonomy.AiStructureBulk` in .NET
and `ATLAS_CAPABILITIES.aiStructureBulk` in TypeScript. It does not change the free
`atlas.ai.structure` capability or its `atlas.ai.structure.daily` allowance.

This additive identifier does not change the schema version or existing grants.
No existing offer grants it automatically; bundle assignment is a separate decision.
Consumers must adopt a published SDK containing this identifier and align their
reserved capability sets before enabling the feature.

## Landscape relation sharing

`atlas.landscape.share.relations` is confirmed as the separate reserved grant. The
suffix keeps it under the existing sharing namespace while identifying the narrower
scope: `runsOn`, `suppliedBy`, a layer hint, and, with explicit stage-2 activation,
`integrates` between included systems/applications. The 2026-10-10 scope decision
extends the existing grant to these undirected company-level associations. Existing
stage-1 activations must not silently expand. Stage 2 requires customer-admin
authorization explicitly covering integrations and a receiver that supports them.
Edges contain only opaque endpoints and a closed kind: no network details, labels,
data descriptions/counts, or personal data. The digest vocabulary does not itself
authorize stage 2; sender and receiver enforce the activated scope.

The .NET identifier is `AtlasTaxonomy.LandscapeShareRelations`; the TypeScript alias
is `ATLAS_CAPABILITIES.landscapeShareRelations`. Both this grant and
`atlas.landscape.share`, plus admin opt-in through an active sharing enrollment,
are required. A signed payload scope is never authorization. The reference evaluator's
`EvaluateRelations(request, relationsEntitlement)` first checks the ordinary sharing
request, then the separate relations entitlement, and bounds validity by both decisions.
Consumers handling relations must use this composed check (or equivalent checks).
The existing `Evaluate` method authorizes the base digest only.

The conservative initial bundle choice grants both capabilities only to Enterprise
and SelfHostedEnterprise while active; lifecycle restrictions remove both. Community,
Hosted Trial, Hosted Starter and Pro do not receive either automatically. Owner question:
should paid tiers below Enterprise or the hosted trial also carry both sharing grants?

This is additive: existing request/schema shapes and base-digest evaluation are unchanged.
Downstream Atlas aliases must align after a containing SDK is published; no downstream
implementation is included here. Latest tag at implementation is `v0.1.9`; the default
patch release would produce `Vev.Fabric.Contracts` and `@vev-software/fabric-contracts`
version `0.1.10` (a minor bump would produce `0.2.0`). The owner chooses and approves
the release; this change does not publish packages or enable integrations.

## Weekli taxonomy

Additive registration (issue #65), mirrored in `WeekliTaxonomy` (.NET), `WEEKLI_CAPABILITIES` and `WEEKLI_LIMIT_KEYS` (TypeScript):

| Identifier | Kind | Unit |
|---|---|---|
| `weekli.planning.extended` | Feature | Extended planning |
| `weekli.ai.text` | Feature | Text AI attempts |
| `weekli.ai.image` | Feature | Image AI attempts |
| `weekli.planning.horizon-days` | Limit | Calendar days from the Monday of the current ISO week, including the current week, in the consumer calendar time zone |
| `weekli.ai.text.attempts.month` | Limit | Dispatched text provider attempts per UTC calendar month |
| `weekli.ai.image.attempts.month` | Limit | Dispatched image provider attempts per UTC calendar month |

An AI attempt counts when a provider request is dispatched, including failed or unknown outcomes. It is not a token, currency amount, successful response, delivered image or user action. An undispatched request does not consume an attempt allowance. A UTC calendar month starts at 00:00 UTC on day one; this is not an agreement-anchored billing month.

No default allowance, price, commercial plan mapping or automatic grant is introduced. The identifiers are not reserved; explicit grants determine availability, including use in a trial. A planning limit is a relative horizon, not a duration in hours and not a cap on historical reads. Existing identifiers, catalogue shape and schema version retain their meanings. Publication follows the normal gated dual-SDK release; adding source identifiers does not publish a package or activate any consumer.

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

## Landscape history

Issue #63 adds vocabulary only:

| Identifier | Kind | SDK aliases | Meaning |
|---|---|---|---|
| `atlas.history.view` | Feature | `AtlasTaxonomy.HistoryView` / `ATLAS_CAPABILITIES.historyView` | Read the landscape as of a date, bounded by the history window. |
| `atlas.history.timeline` | Feature, reserved | `AtlasTaxonomy.HistoryTimeline` / `ATLAS_CAPABILITIES.historyTimeline` | Extension for unlimited look-back, comparison between dates and baselines. |
| `atlas.history.window-months` | Limit | `AtlasTaxonomy.HistoryWindowMonths` / `ATLAS_LIMIT_KEYS.historyWindowMonths` | Maximum look-back in calendar months for the as-of view. |

The proposed spelling `atlas.history.windowMonths` is normalized to
`atlas.history.window-months` to follow the existing lowercase taxonomy rules.
There is no alias with the mixed-case spelling and no existing identifier is renamed.

Registration does not grant access, assign a plan, set a default month count or enable
history/timeline behavior. Reserved describes the extension seam; it does not grant
unlimited access by itself. Consumers apply their entitlement and limit decisions.
No existing bundle or grant changes. The addition preserves contract version 1 and
requires a separate approved SDK release and consumer adoption before use.
