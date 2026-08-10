namespace Vev.Fabric.Contracts.Entitlements;

/// <summary>
/// Request to evaluate whether a tenant may use a capability.
/// </summary>
public readonly record struct EntitlementRequest(
    TenantContext Tenant,
    CapabilityId Capability,
    PrincipalContext Principal,
    ResourceId? Resource = null);

/// <summary>
/// Local evaluator abstraction used by products on the request path.
/// </summary>
public interface IEntitlementService
{
    EntitlementDecision Evaluate(EntitlementRequest request);
}

/// <summary>
/// Public API payload for evaluating one entitlement request.
/// </summary>
public sealed record EvaluateEntitlementsRequest(
    IReadOnlyList<EntitlementRequest> Requests);

/// <summary>
/// Public API payload returned from single or batch entitlement evaluation.
/// </summary>
public sealed record EvaluateEntitlementsResponse(
    IReadOnlyList<EntitlementDecision> Decisions);
