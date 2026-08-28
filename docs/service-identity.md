# Fabric Service Identity — machine-to-machine callers

The service-identity contract (`Vev.Fabric.Contracts.Identity`). Where
[`PrincipalContext`](identity.md) models an authenticated **user**, this models an
authenticated **service**: one product backend calling a sibling product's API on a tenant's
behalf. Identity stays a Fabric concern — a product never invents its own machine-auth model.

## Why it exists

A service call between two products needs an identity the callee can verify. The weak way to
do that is a single long-lived shared secret sent on every request: whoever holds it can
impersonate the caller for **any** tenant, the callee must store the secret too, and rotating
it is a coordinated outage. This contract replaces that with a short-lived, **asymmetrically
signed** assertion:

- the **caller** holds a private signing key and mints a fresh assertion per call;
- the **callee** holds only the caller's **public** key and verifies the signature;
- the assertion names the **one tenant** the call acts for, so a leaked assertion is scoped to
  a single tenant and expires in minutes rather than being an all-tenants master credential.

It mirrors the substrate's existing signed-document convention
([`SignedEntitlementSnapshot`](entitlements.md)) but is asymmetric (ECDSA), not HMAC.

## The wire form

A standard compact JWS — `base64url(header).base64url(payload).base64url(signature)` — carried
in the `X-Fabric-Service-Assertion` header, signed with **ES256** (ECDSA P-256 / SHA-256). The
algorithm is **pinned**: a verifier honours `ES256` and nothing else (no `alg` negotiation, no
`none` downgrade, no HMAC confusion).

```jsonc
// payload
{
  "iss": "vev:service/caller",               // who minted it
  "aud": "vev:service/callee",                // who it is for
  "sub": "<service-principal-id>",           // the acting service principal
  "tenant": "tenant-a",                       // the ONE tenant this call acts for
  "roles": ["catalogue.write"],              // coarse roles the callee maps to authorization
  "iat": 1793664000, "nbf": 1793664000, "exp": 1793664300,  // short-lived
  "jti": "…"                                  // unique per assertion
}
```

## Using it

```csharp
// Caller (mints per request) — holds the private key.
var issuer = ServiceAssertionIssuer.FromPem(
    issuer: "vev:service/caller", keyId: "caller-2026", privateKeyPem: pem);
var header = issuer.Issue(
    audience: "vev:service/callee", subject: "svc-worker",
    tenantId: tenant.TenantId, roles: ["catalogue.write"], lifetime: TimeSpan.FromMinutes(5));
request.Headers.Add(ServiceIdentity.AssertionHeaderName, header);

// Callee (verifies) — holds only the public key.
var validator = ServiceAssertionValidator.FromPem(
    keyId: "caller-2026", publicKeyPem: pem,
    expectedIssuer: "vev:service/caller", expectedAudience: "vev:service/callee");
var result = validator.Validate(request.Headers[ServiceIdentity.AssertionHeaderName]);
if (!result.IsValid) return Reject(result.ReasonCode);       // fail-closed, reason-coded
var principal = result.Assertion!.ToPrincipalContext();      // bind the per-request identity
var actingTenant = result.Assertion!.Tenant;                 // scoped to one tenant
```

## Verification is fail-closed

`Validate` returns a specific reason code and never a default-accept. Signature is checked
**before** any claim is trusted, and only against the trusted public key selected by `kid`:

| Reason code | Meaning |
|---|---|
| `service_assertion_valid` | verified |
| `service_assertion_malformed` | not a three-segment compact JWS / undecodable |
| `service_assertion_unsupported_algorithm` | header `alg` is not `ES256` |
| `service_assertion_unknown_key` | `kid` is not a trusted key |
| `service_assertion_bad_signature` | signature does not verify under the trusted key |
| `service_assertion_expired` / `_not_yet_valid` | outside `exp` / `nbf` (± clock skew) |
| `service_assertion_wrong_issuer` / `_wrong_audience` | `iss` / `aud` mismatch |
| `service_assertion_missing_tenant` | no `tenant` claim |

## Key management and rotation

The caller's private key never leaves the caller; the callee is configured with the caller's
public key under a `kid`. To rotate, publish a new key under a new `kid`, have the callee trust
**both** old and new `kid`s (the validator takes a `kid → public key` map) during an overlap
window, then switch the caller to the new `kid` and drop the old — no shared secret, no
synchronized cutover.

## Scope of this contract

This is the pure identity assertion — mint, verify, and the claim/header/reason-code
vocabulary — and is deliberately transport- and product-neutral. It does not decide *what* a
service principal may do: that stays with [authorization](authorization.md) (`IAuthorizer` over
the resulting `roles`). A future central issuer (a control-plane token endpoint) can replace
the self-minting caller without changing the callee's verification — the verifier already only
needs a trusted public key and the pinned algorithm.
