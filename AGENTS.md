# AGENTS.md — repo-local guardrails for fabric

This repository is public. Read `Vev-software/engineering/AGENTS.md` for the full
policy; this file narrows it to what matters most here and adds the release ritual.
Where this file and the handbook differ, the handbook wins.

## What this repo is

`fabric` is the public contract layer of the VEV substrate: a **dual-SDK package** —
`Vev.Fabric.Contracts` (.NET, `src/Vev.Fabric.Contracts/`) and
`@vev-software/fabric-contracts` (npm, `sdk/typescript/`) — plus the authoritative JSON
Schemas in `schemas/v1/`. It defines cross-cutting concerns (tenant/principal context,
entitlements, authorization, tenant lifecycle, taxonomy, the audit event envelope) and
never a product domain. The dependency-direction fitness check enforces that: Fabric must
not reference Atlas, Portic or Orion.

## Releasing this package — the git tag is the single source of truth (18 §1.1)

There is **no version number to edit** in this repo:

- The .NET version comes from **MinVer**; the npm version is stamped from the tag in
  CI. The committed `package.json` `version` is a `0.0.0` placeholder.
- To cut a release, use the **one-button flow** — no local git/tag commands:
  Actions → **release** (`publish.yml`) → **Run workflow** → pick the bump
  (patch/minor/major) → Run. It validates that you are on `main` and that the latest
  `ci` run for `main`'s HEAD is green, then pauses on the `release` environment for a
  single **Approve** click, and on approval pushes the tag `vX.Y.Z` (which fires the
  `github release` workflow, `release.yml`) and publishes to nuget.org + npm via OIDC.
  Use the `dry_run` checkbox to rehearse without tagging or publishing.
- **Never** add a `<VersionPrefix>`/`<Version>` or a real `package.json` version.
- Release-logic placement: the **GitHub Release** stays a thin caller of the org
  reusable workflow (`release.yml`). The **registry publish is inlined** in
  `publish.yml` — this is deliberate and required: NuGet Trusted Publishing rejects an
  org-owned policy across a reusable-workflow boundary ([NuGet/login#6]), so the nuget
  job must run in this repo; npm rides along in the same gated job so one Approve covers
  the whole release. Keep `publish.yml` named exactly that — both Trusted Publisher
  policies pin `workflow=publish.yml`.

[NuGet/login#6]: https://github.com/NuGet/login/issues/6

## Public disclosure rules

- Public PR titles/bodies, issue bodies, README/docs, ADRs and `.github` templates
  describe only this repo's code/behaviour and its published public contracts.
- Do **not** include: private repo/module names, proprietary deployment topology or
  control paths, licence-enforcement/entitlement detail, internal hostnames, customer
  names, security-control specifics, or secrets/credentials.
- Security vulnerabilities do not belong in a public issue/PR — follow `SECURITY.md`.

## Boundaries

- Public, Apache-2.0. The community build must never require a private repo or feed
  (`§1.9`). Depend on contracts, not implementations (`§1.2`).
- Breaking a public contract is expensive: it needs an ADR, a migration path, a
  deprecation period and compatibility tests (`§4`, `03 · E3`).
- Material or cross-cutting changes start as an issue or ADR, not a surprise PR.
