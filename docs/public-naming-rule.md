# Public naming rule

This repository is public. It defines shared vocabulary (ids, formats, SDKs); it is not a
place to announce products or to describe commercial terms.

## What may appear in public files

- **Product names and capability ids** only at or after the product's public launch. Until
  then, examples, tests, fixtures and docs use neutral ids.
- **Neutral ids** for examples: `product-a`, `product-b` for `productId`, and
  `offer-family`-style opaque values for offers. The one real product id already public is
  `weekli`.
- **Never**: prices, plans, discounts, coupons, currency amounts, customer data or customer
  names, in any file, PR title or PR body.

## How it is enforced

`scripts/check-public-naming.mjs` runs in the `ci` workflow and fails the pull request.

1. **Structural checks** (no secret list needed):
   - every `productId` value must be listed in `.github/public-naming-allowlist.txt`;
   - the namespace (text before the first dot) of every capability or limit id in the
     taxonomy sources and SDKs must be listed in `.github/public-naming-namespaces.txt`;
   - commercial leakage patterns (currency amounts, per-month pricing, coupons, discount
     offers) are rejected. Neutral wording such as "no price" is fine.
2. **Deny-list check**: maintainers can supply an additional list of terms through the
   `PUBLIC_NAMING_DENYLIST` repository secret. It is never stored in the repository. When
   the list is empty (forks, local runs) this layer is skipped. When it is set, tracked
   files, file names and the pull request title and body are scanned; findings are reported
   as file and line with the matched term redacted.

The checks are a safety net, not a substitute for review. They cannot see names that are not
on the deny-list, paraphrases, or text in places other than tracked files and the PR text
(for example commit messages, issue comments and review comments).

## Making a product public

At launch, a reviewed pull request adds the product's id to
`.github/public-naming-allowlist.txt` and, if its capability ids use a new namespace, that
namespace to `.github/public-naming-namespaces.txt`. Nothing else makes a name public.

## Running locally

```bash
node scripts/check-public-naming.mjs
node --test tests/public-naming/
```

`tests/public-naming/` holds the guard's own tests and a deliberately bad fixture; it uses
dummy names only and is excluded from the structural scan.
