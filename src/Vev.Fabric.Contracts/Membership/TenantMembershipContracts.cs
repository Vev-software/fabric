using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Membership;

/// <summary>
/// Issuer-qualified identity of a tenant member. Both parts are compared exactly (ordinal); a
/// subject is only unique within its issuer, and neither part is an email or a credential.
/// </summary>
/// <param name="Issuer">Identity provider issuer (the OIDC <c>iss</c>).</param>
/// <param name="Subject">Stable subject within that issuer (the OIDC <c>sub</c>).</param>
public sealed record TenantMemberIdentity(string Issuer, string Subject);

/// <summary>
/// One member and the opaque role names bound to it. What a role means is product policy.
/// </summary>
public sealed record TenantMember(TenantMemberIdentity Identity, IReadOnlyCollection<string> Roles);

/// <summary>
/// Portable, versioned, authoritative membership of one tenant (fabric#57). Data only: it carries
/// no credentials, no transport and no role policy, and is applied independently of how it was
/// authenticated. Absence from <see cref="Members"/> means "not a member".
/// </summary>
/// <param name="SchemaVersion">Contract version; consumers reject versions they do not know.</param>
/// <param name="Tenant">The tenant this membership is scoped to.</param>
/// <param name="Sequence">Monotonic revision within the tenant, starting at 1.</param>
/// <param name="IssuedAt">When the snapshot was produced.</param>
/// <param name="ValidUntil">When the snapshot stops conferring membership.</param>
/// <param name="Members">The complete membership.</param>
public sealed record TenantMembershipSnapshot(
    int SchemaVersion,
    TenantContext Tenant,
    long Sequence,
    DateTimeOffset IssuedAt,
    DateTimeOffset ValidUntil,
    IReadOnlyCollection<TenantMember> Members)
{
    /// <summary>The only schema version this release understands.</summary>
    public const int CurrentSchemaVersion = 1;
}

/// <summary>
/// Outcome of applying a snapshot.
/// </summary>
public sealed record TenantMembershipApplyResult(bool Accepted, string ReasonCode)
{
    public static TenantMembershipApplyResult Accept() => new(true, ReasonCodes.Allow);

    public static TenantMembershipApplyResult Reject(string reasonCode) => new(false, reasonCode);
}

/// <summary>
/// Outcome of looking up one identity. <see cref="Roles"/> is empty unless <see cref="IsMember"/>.
/// </summary>
public sealed record TenantMembershipResolution(bool IsMember, IReadOnlyCollection<string> Roles, string ReasonCode)
{
    public static TenantMembershipResolution Member(IReadOnlyCollection<string> roles) =>
        new(true, roles, ReasonCodes.Allow);

    public static TenantMembershipResolution Denied(string reasonCode) => new(false, [], reasonCode);
}

/// <summary>
/// Reference consumer of <see cref="TenantMembershipSnapshot"/>. A product may use it as is or
/// reimplement the same rules; either way the semantics below are the contract.
/// </summary>
/// <remarks>
/// Fail closed: nothing is a member before a snapshot has been applied, after
/// <see cref="TenantMembershipSnapshot.ValidUntil"/>, or when the clock has moved backwards past
/// the last observation. Authenticating the snapshot and any clock-skew tolerance belong to the
/// caller; this type only applies already-trusted data.
/// </remarks>
public sealed class TenantMembershipRegistry(string tenantId, TimeProvider? clock = null)
{
    private readonly object _gate = new();
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;
    private TenantMembershipSnapshot? _current;
    private Dictionary<(string Issuer, string Subject), string[]> _index = [];
    private DateTimeOffset _observedAt = DateTimeOffset.MinValue;

    /// <summary>Validate a snapshot against this registry's tenant and state, then adopt it.</summary>
    public TenantMembershipApplyResult Apply(TenantMembershipSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        lock (_gate)
        {
            if (Observe() is not { } now)
            {
                return TenantMembershipApplyResult.Reject(ReasonCodes.EntitlementClockRegression);
            }

            if (snapshot.SchemaVersion != TenantMembershipSnapshot.CurrentSchemaVersion)
            {
                return TenantMembershipApplyResult.Reject(ReasonCodes.MembershipSnapshotUnsupportedVersion);
            }

            if (!string.Equals(snapshot.Tenant.TenantId, tenantId, StringComparison.Ordinal))
            {
                return TenantMembershipApplyResult.Reject(ReasonCodes.MembershipSnapshotTenantMismatch);
            }

            if (!TryBuildIndex(snapshot, out var index))
            {
                return TenantMembershipApplyResult.Reject(ReasonCodes.MembershipSnapshotInvalid);
            }

            if (_current is not null)
            {
                if (snapshot.Sequence < _current.Sequence)
                {
                    return TenantMembershipApplyResult.Reject(ReasonCodes.MembershipSnapshotRolledBack);
                }

                if (snapshot.Sequence == _current.Sequence && !SameContent(_current, _index, snapshot, index))
                {
                    return TenantMembershipApplyResult.Reject(ReasonCodes.MembershipSnapshotInvalid);
                }
            }

            if (now >= snapshot.ValidUntil)
            {
                return TenantMembershipApplyResult.Reject(ReasonCodes.MembershipSnapshotExpired);
            }

            _current = snapshot;
            _index = index;
            return TenantMembershipApplyResult.Accept();
        }
    }

    /// <summary>Look up an identity at the current time.</summary>
    public TenantMembershipResolution Resolve(string issuer, string subject)
    {
        lock (_gate)
        {
            if (Observe() is not { } now)
            {
                return TenantMembershipResolution.Denied(ReasonCodes.EntitlementClockRegression);
            }

            if (_current is null)
            {
                return TenantMembershipResolution.Denied(ReasonCodes.MembershipSnapshotUnavailable);
            }

            if (now >= _current.ValidUntil)
            {
                return TenantMembershipResolution.Denied(ReasonCodes.MembershipSnapshotExpired);
            }

            return _index.TryGetValue((issuer, subject), out var roles)
                ? TenantMembershipResolution.Member(roles)
                : TenantMembershipResolution.Denied(ReasonCodes.MembershipNotFound);
        }
    }

    private DateTimeOffset? Observe()
    {
        var now = _clock.GetUtcNow();
        if (now < _observedAt)
        {
            return null;
        }

        _observedAt = now;
        return now;
    }

    private static bool TryBuildIndex(
        TenantMembershipSnapshot snapshot,
        out Dictionary<(string Issuer, string Subject), string[]> index)
    {
        index = [];

        if (snapshot.Sequence < 1 || snapshot.ValidUntil <= snapshot.IssuedAt || snapshot.Members is null)
        {
            return false;
        }

        foreach (var member in snapshot.Members)
        {
            if (member?.Identity is not { } identity
                || string.IsNullOrWhiteSpace(identity.Issuer)
                || string.IsNullOrWhiteSpace(identity.Subject)
                || member.Roles is null
                || member.Roles.Count == 0
                || member.Roles.Any(string.IsNullOrWhiteSpace)
                || member.Roles.Distinct(StringComparer.Ordinal).Count() != member.Roles.Count
                || !index.TryAdd((identity.Issuer, identity.Subject), [.. member.Roles]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SameContent(
        TenantMembershipSnapshot left,
        Dictionary<(string Issuer, string Subject), string[]> leftIndex,
        TenantMembershipSnapshot right,
        Dictionary<(string Issuer, string Subject), string[]> rightIndex) =>
        left.IssuedAt == right.IssuedAt
        && left.ValidUntil == right.ValidUntil
        && leftIndex.Count == rightIndex.Count
        && leftIndex.All(pair => rightIndex.TryGetValue(pair.Key, out var roles)
            && pair.Value.Order(StringComparer.Ordinal).SequenceEqual(roles.Order(StringComparer.Ordinal)));
}
