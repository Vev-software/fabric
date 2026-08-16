# Contributing to fabric

Thanks for helping build the VEV substrate. `fabric` is the **public contract layer**
(Apache-2.0): schemas, the .NET/TypeScript SDKs and conformance kit that products build
against. Contributions are welcome under the terms below.

## Sign-off: DCO required

Because VEV runs an **open-core model**, we need the right to use every contribution across
the open and commercial editions (handbook `17 §3`). This repository uses the
**[Developer Certificate of Origin](https://developercertificate.org/)** (DCO), not a CLA:
its Apache-2.0 licence already grants the rights the open-core model needs, so a lightweight
sign-off is enough. (A CLA is required only in the AGPL/BSL **runtime** repos, which state so
themselves.)

Every commit must carry a `Signed-off-by:` line matching the author:

```bash
git commit -s -m "your message"
```

By signing off you certify the DCO: you wrote the change (or have the right to submit it) and
agree it may be provided under this repository's licence. A commit without a sign-off cannot be
merged; if you forget, `git commit --amend -s` (or a rebase sign-off) fixes it.

## What makes a good change

- **One logical change per PR.** Small, reviewable, with a clear title.
- **Green CI.** Build, tests and conformance must pass; run `dotnet build` + `dotnet test` and
  the TypeScript build locally first.
- **Contract changes carry conformance.** New or changed schema? Add a sample fixture and a
  test that proves the SDK round-trips it, and update the matching .NET and TS types so all
  three (schema, .NET, TS) stay in lock-step.
- **Docs + compatibility note.** Document new contracts under `docs/`, and state the
  compatibility impact.

## The boundary (what belongs here)

`fabric` is foundation contracts only — identity, tenancy, entitlement, authorization, audit,
the extension model and reason codes:

- **No product domain.** If a contract knows what an "application portfolio" is, it belongs in
  a product, not here (`05 §3`).
- **Everything points down.** Fabric must never depend on Atlas, Portic or Orion
  (`AGENTS.md §1.1`); the dependency-direction check fails the build if it does.
- **No product-specific ids in shared code.** Products contribute ids in their own namespace.

## Versioning

Public contracts follow SemVer. **Additive** changes (new optional fields, new enum members)
are non-breaking. **Breaking** a published contract — removing/renaming a field, tightening a
constraint, changing a value's meaning — requires an ADR, a migration path and a deprecation
period (handbook `05 §7`, `18`). See the `architecture` repository for the decision records.

## Security

Please report vulnerabilities **privately**, never through a public issue: use the
repository's **Security** tab → **Report a vulnerability**.

## Licence

By contributing you agree your work is provided under the repository's
[Apache-2.0](LICENSE) licence.
