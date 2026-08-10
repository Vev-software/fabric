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
