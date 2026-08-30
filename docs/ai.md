# Fabric AI contract

Fabric owns the provider-neutral contract for advisory AI assistance. Products submit an
`AiAssistRequest`; Portic or another adapter implements `IAiAssistService` behind that seam.

Every request carries the mandatory policy: no provider training on customer data, EEA residency,
redaction before provider routing, and prompt-injection handling. An implementation must fail
closed when those requirements cannot be met. `UnavailableAiAssistService` is the reference
default for deployments without an approved provider.

`AiAssistResult` includes a policy attestation and an optional usage record. Products use the usage
record for their own entitlement/metering ledger and audit trail. The contract neither evaluates
entitlements nor mutates product state.

AI output is advisory. Each product owns its validation, approval and publication workflow; an AI
result must never bypass a human-gated state transition.
