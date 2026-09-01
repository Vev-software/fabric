using Vev.Fabric.Contracts.Lifecycle;

namespace Vev.Fabric.Contracts.Entitlements;

/// <summary>
/// Bridges the canonical hosted lifecycle contract to the lifecycle states used during entitlement
/// bundle translation.
/// </summary>
public static class EntitlementLifecycleStateMapper
{
    public static EntitlementLifecycleState From(TenantLifecycleStatus lifecycle) =>
        From(lifecycle.State);

    public static EntitlementLifecycleState From(TenantLifecycleState lifecycle) =>
        lifecycle switch
        {
            TenantLifecycleState.TrialActive => EntitlementLifecycleState.TrialActive,
            TenantLifecycleState.TrialExpired => EntitlementLifecycleState.TrialExpired,
            TenantLifecycleState.ReadOnly => EntitlementLifecycleState.ReadOnly,
            TenantLifecycleState.Locked => EntitlementLifecycleState.Locked,
            TenantLifecycleState.RetentionPeriod => EntitlementLifecycleState.RetentionPeriod,
            TenantLifecycleState.DataPurged => EntitlementLifecycleState.DataPurged,
            _ => EntitlementLifecycleState.Active
        };
}
