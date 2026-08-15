using System.Collections.Concurrent;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Authorization;

/// <summary>
/// Request to authorize whether a principal may perform a coarse action on a resource within a tenant.
/// </summary>
public readonly record struct AuthorizationRequest(
    TenantContext Tenant,
    PrincipalContext Principal,
    string Action,
    ResourceId Resource);

/// <summary>
/// The result of an authorization decision, with a shared machine-readable reason code.
/// </summary>
public sealed record AuthorizationDecision(
    bool Allowed,
    string Action,
    ResourceId Resource,
    string ReasonCode,
    string Source)
{
    public static AuthorizationDecision Allow(string action, ResourceId resource, string source) =>
        new(true, action, resource, ReasonCodes.Allow, source);

    public static AuthorizationDecision Deny(string action, ResourceId resource, string reasonCode, string source) =>
        new(false, action, resource, reasonCode, source);
}

/// <summary>
/// Fabric-owned authorization mechanism: may a principal perform an action on a resource.
/// </summary>
public interface IAuthorizer
{
    AuthorizationDecision Authorize(AuthorizationRequest request);
}

/// <summary>
/// Registry a product uses to declare role requirements without owning the authorization engine.
/// </summary>
public sealed class AuthorizationPolicyRegistry
{
    private readonly ConcurrentDictionary<string, string[]> _actionRequiredRoles = new(StringComparer.Ordinal);

    /// <summary>
    /// Declare that <paramref name="action"/> requires the principal to hold any of <paramref name="anyOfRoles"/>.
    /// </summary>
    public AuthorizationPolicyRegistry Require(string action, params string[] anyOfRoles)
    {
        _actionRequiredRoles[action] = anyOfRoles;
        return this;
    }

    internal bool TryGetRequiredRoles(string action, out string[] roles) =>
        _actionRequiredRoles.TryGetValue(action, out roles!);
}

/// <summary>
/// Simple local reference implementation for development and tests.
/// </summary>
public sealed class LocalAuthorizer(AuthorizationPolicyRegistry policies) : IAuthorizer
{
    public const string DefaultSource = "local-authorizer";

    public AuthorizationDecision Authorize(AuthorizationRequest request)
    {
        if (!policies.TryGetRequiredRoles(request.Action, out var requiredRoles) || requiredRoles.Length == 0)
        {
            return AuthorizationDecision.Allow(request.Action, request.Resource, DefaultSource);
        }

        var holdsRequiredRole = request.Principal.Roles.Any(role => requiredRoles.Contains(role, StringComparer.Ordinal));
        return holdsRequiredRole
            ? AuthorizationDecision.Allow(request.Action, request.Resource, DefaultSource)
            : AuthorizationDecision.Deny(request.Action, request.Resource, ReasonCodes.RoleMissing, DefaultSource);
    }
}
