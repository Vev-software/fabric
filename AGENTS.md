# AGENTS.md — repo-local guardrails for fabric

This repository is public. Read `Vev-software/engineering/AGENTS.md` for the full
policy; this file narrows it to what matters most here and adds the release ritual.
Where this file and the handbook differ, the handbook wins.

## Fabric is the base of the dependency graph

Everything points **down**: products (Portic, Atlas, Orion) depend on Fabric
contracts; **Fabric must never depend on a product** (`03 · B1`, `02 §7`). Fabric may
not know what an "application portfolio" or a "gateway route" is. The
`check-dependency-direction` fitness check enforces this — do not weaken it.

## Releasing this package — the git tag is the single source of truth (18 §1.1)

Fabric ships a dual SDK: `Vev.Fabric.Contracts` (.NET) and
`@vev-software/fabric-contracts` (npm), both bundling `schemas/v1`. There is **no
version number to edit** in this repo:

- The .NET version comes from **MinVer**; the npm version is stamped from the tag in
  CI. The committed `package.json` `version` is a `0.0.0` placeholder.
- To cut a release: `git tag vX.Y.Z && git push origin vX.Y.Z` (SemVer). That runs
  `release.yml` → a signed GitHub Release. Then run `publish.yml` with the **same
  tag** to push to the registries (manual, gated by the `release` environment).
- **Never** add a `<VersionPrefix>`/`<Version>` or a real `package.json` version, and
  **never** inline the release logic — it lives in the org's reusable workflows
  (`Vev-software/.github`). This repo only has thin callers.

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
- Material or cross-cutting changes start as an issue or ADR, not a surprise PR.
