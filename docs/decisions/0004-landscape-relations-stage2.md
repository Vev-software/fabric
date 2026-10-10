# Landscape relations grant — stage-2 scope decision

Status: Accepted, 2026-10-10 (owner decision).

The existing reserved `atlas.landscape.share.relations` capability may cover
undirected `integrates` associations between included system/application components,
in addition to `runsOn`, `suppliedBy` and the layer hint. This is the explicit scope
decision anticipated in the original capability definition. No identifier, bundle,
wire schema or entitlement evaluator signature changes.

Customer-admin activation must explicitly include integration associations; an
existing stage-1 activation does not automatically expand. Both sender and receiver
must support stage 2 and enforce the trusted activation scope, base sharing grant,
separate relations grant and active sharing enrollment. Payload scope cannot grant
permission. Integration edges carry only opaque endpoints and the enumerated kind,
with no protocol, address, port, host, URL, label, data text/count or personal data.

Compatibility: existing grant identifiers and SDK aliases remain stable; stage-1
payloads and evaluations are unchanged. Consumers supporting only stage 1 may
continue rejecting stage-2 payloads. Roll out receiver support and explicit activation
before exporting stage 2. Publishing a package does not activate customer sharing.
