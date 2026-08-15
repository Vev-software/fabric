using System.Text.Json.Serialization;
using Vev.Fabric.Contracts.Audit;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Discovery;

[JsonConverter(typeof(JsonStringEnumConverter<DiscoveryEnrollmentState>))]
/// <summary>
/// Canonical lifecycle states for a tenant-scoped discovery machine enrollment.
/// </summary>
public enum DiscoveryEnrollmentState
{
    Pending,
    Active,
    Suspended,
    Revoked,
    Expired
}

[JsonConverter(typeof(JsonStringEnumConverter<DiscoveryEnrollmentTransition>))]
/// <summary>
/// Explicit lifecycle transitions Fabric may apply to a discovery enrollment.
/// </summary>
public enum DiscoveryEnrollmentTransition
{
    Activate,
    RotateCredential,
    Suspend,
    Revoke
}

[JsonConverter(typeof(JsonStringEnumConverter<DiscoveryLifecycleEventType>))]
/// <summary>
/// Lifecycle event types emitted around discovery enrollment and access.
/// </summary>
public enum DiscoveryLifecycleEventType
{
    EnrollmentCreated,
    EnrollmentActivated,
    CredentialRotated,
    AccessDenied,
    EnrollmentSuspended,
    EnrollmentRevoked,
    CredentialExpired
}

/// <summary>
/// Timeline carried by the public discovery enrollment contract.
/// </summary>
public sealed record DiscoveryEnrollmentTimeline(
    DateTimeOffset EnrolledAt,
    DateTimeOffset? CredentialExpiresAt = null,
    DateTimeOffset? ActivatedAt = null,
    DateTimeOffset? LastRotatedAt = null,
    DateTimeOffset? SuspendedAt = null,
    DateTimeOffset? RevokedAt = null);

/// <summary>
/// Query the current discovery enrollment state for a tenant-scoped machine principal.
/// </summary>
public sealed record DiscoveryEnrollmentQuery(
    string EnrollmentId,
    TenantContext Tenant,
    PrincipalContext Principal,
    CapabilityId Capability,
    DateTimeOffset? AsOf = null);

/// <summary>
/// The current discovery enrollment state the scanner/bootstrap runtime consumes.
/// </summary>
public sealed record DiscoveryEnrollmentStatus(
    string EnrollmentId,
    TenantContext Tenant,
    PrincipalContext Principal,
    CapabilityId Capability,
    DiscoveryEnrollmentState State,
    string ReasonCode,
    DateTimeOffset EvaluatedAt,
    DiscoveryEnrollmentTimeline Timeline);

/// <summary>
/// Apply one explicit discovery enrollment transition to the current timeline.
/// </summary>
public sealed record DiscoveryEnrollmentTransitionRequest(
    string EnrollmentId,
    TenantContext Tenant,
    PrincipalContext Principal,
    CapabilityId Capability,
    DiscoveryEnrollmentTransition Transition,
    DateTimeOffset OccurredAt,
    DiscoveryEnrollmentTimeline Timeline,
    DateTimeOffset? CredentialExpiresAt = null);

/// <summary>
/// Result of attempting one discovery enrollment transition.
/// </summary>
public sealed record DiscoveryEnrollmentTransitionResult(
    bool Accepted,
    string ReasonCode,
    DiscoveryEnrollmentStatus Enrollment);

/// <summary>
/// Public lifecycle event emitted around discovery enrollment and denied access attempts.
/// </summary>
public sealed record DiscoveryLifecycleEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    TenantContext Tenant,
    string EnrollmentId,
    string PrincipalId,
    string Source,
    DiscoveryLifecycleEventType EventType,
    string ReasonCode,
    CapabilityId Capability,
    string CorrelationId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>
/// Shared evaluation and transition validation for a discovery machine enrollment.
/// This stays fail-static: if the credential is expired or the enrollment is not active,
/// discovery ingestion must be denied rather than downgraded.
/// </summary>
public static class DiscoveryEnrollmentStateMachine
{
    public static DiscoveryEnrollmentStatus Evaluate(DiscoveryEnrollmentQuery query, DiscoveryEnrollmentTimeline timeline) =>
        Evaluate(query.EnrollmentId, query.Tenant, query.Principal, query.Capability, timeline, query.AsOf ?? DateTimeOffset.UtcNow);

    public static DiscoveryEnrollmentStatus Evaluate(
        string enrollmentId,
        TenantContext tenant,
        PrincipalContext principal,
        CapabilityId capability,
        DiscoveryEnrollmentTimeline timeline,
        DateTimeOffset asOf)
    {
        ValidateTimeline(timeline);

        return timeline switch
        {
            { RevokedAt: not null } when asOf >= timeline.RevokedAt.Value =>
                new DiscoveryEnrollmentStatus(enrollmentId, tenant, principal, capability, DiscoveryEnrollmentState.Revoked, ReasonCodes.DiscoveryEnrollmentRevoked, asOf, timeline),

            { SuspendedAt: not null } when asOf >= timeline.SuspendedAt.Value =>
                new DiscoveryEnrollmentStatus(enrollmentId, tenant, principal, capability, DiscoveryEnrollmentState.Suspended, ReasonCodes.DiscoveryEnrollmentSuspended, asOf, timeline),

            { CredentialExpiresAt: not null } when asOf >= timeline.CredentialExpiresAt.Value =>
                new DiscoveryEnrollmentStatus(enrollmentId, tenant, principal, capability, DiscoveryEnrollmentState.Expired, ReasonCodes.DiscoveryCredentialExpired, asOf, timeline),

            { ActivatedAt: not null } when asOf >= timeline.ActivatedAt.Value =>
                new DiscoveryEnrollmentStatus(enrollmentId, tenant, principal, capability, DiscoveryEnrollmentState.Active, ReasonCodes.Allow, asOf, timeline),

            _ =>
                new DiscoveryEnrollmentStatus(enrollmentId, tenant, principal, capability, DiscoveryEnrollmentState.Pending, ReasonCodes.DiscoveryEnrollmentPending, asOf, timeline)
        };
    }

    public static DiscoveryEnrollmentTransitionResult Apply(DiscoveryEnrollmentTransitionRequest request)
    {
        var current = Evaluate(request.EnrollmentId, request.Tenant, request.Principal, request.Capability, request.Timeline, request.OccurredAt);
        var updatedTimeline = request.Timeline;

        switch (request.Transition)
        {
            case DiscoveryEnrollmentTransition.Activate:
                if (current.State != DiscoveryEnrollmentState.Pending ||
                    request.CredentialExpiresAt is null ||
                    request.CredentialExpiresAt <= request.OccurredAt)
                {
                    return Reject(current);
                }

                updatedTimeline = request.Timeline with
                {
                    ActivatedAt = request.OccurredAt,
                    CredentialExpiresAt = request.CredentialExpiresAt
                };
                break;

            case DiscoveryEnrollmentTransition.RotateCredential:
                if (current.State != DiscoveryEnrollmentState.Active ||
                    request.CredentialExpiresAt is null ||
                    request.CredentialExpiresAt <= request.OccurredAt)
                {
                    return Reject(current);
                }

                updatedTimeline = request.Timeline with
                {
                    CredentialExpiresAt = request.CredentialExpiresAt,
                    LastRotatedAt = request.OccurredAt
                };
                break;

            case DiscoveryEnrollmentTransition.Suspend:
                if (current.State != DiscoveryEnrollmentState.Active)
                {
                    return Reject(current);
                }

                updatedTimeline = request.Timeline with { SuspendedAt = request.OccurredAt };
                break;

            case DiscoveryEnrollmentTransition.Revoke:
                if (current.State == DiscoveryEnrollmentState.Revoked)
                {
                    return Reject(current);
                }

                updatedTimeline = request.Timeline with { RevokedAt = request.OccurredAt };
                break;

            default:
                return Reject(current);
        }

        var resultingEnrollment = Evaluate(request.EnrollmentId, request.Tenant, request.Principal, request.Capability, updatedTimeline, request.OccurredAt);
        return new DiscoveryEnrollmentTransitionResult(true, resultingEnrollment.ReasonCode, resultingEnrollment);
    }

    private static DiscoveryEnrollmentTransitionResult Reject(DiscoveryEnrollmentStatus current) =>
        new(false, ReasonCodes.DiscoveryLifecycleTransitionInvalid, current);

    private static void ValidateTimeline(DiscoveryEnrollmentTimeline timeline)
    {
        if (timeline.CredentialExpiresAt is not null && timeline.CredentialExpiresAt < timeline.EnrolledAt)
        {
            throw new ArgumentException("credentialExpiresAt must be on or after enrolledAt.");
        }

        if (timeline.ActivatedAt is not null && timeline.ActivatedAt < timeline.EnrolledAt)
        {
            throw new ArgumentException("activatedAt must be on or after enrolledAt.");
        }

        if (timeline.LastRotatedAt is not null && timeline.LastRotatedAt < timeline.EnrolledAt)
        {
            throw new ArgumentException("lastRotatedAt must be on or after enrolledAt.");
        }

        if (timeline.SuspendedAt is not null && timeline.ActivatedAt is null)
        {
            throw new ArgumentException("suspendedAt requires an activated enrollment.");
        }

        if (timeline.SuspendedAt is not null && timeline.SuspendedAt < timeline.ActivatedAt)
        {
            throw new ArgumentException("suspendedAt must be on or after activatedAt.");
        }

        if (timeline.RevokedAt is not null && timeline.RevokedAt < timeline.EnrolledAt)
        {
            throw new ArgumentException("revokedAt must be on or after enrolledAt.");
        }
    }
}

/// <summary>
/// Shared action and resource vocabulary for emitting discovery audit events through the
/// Fabric-owned <see cref="AuditEvent"/> envelope.
/// </summary>
public static class DiscoveryAuditVocabulary
{
    public const string EnrollmentCreateAction = "fabric.discovery.enrollment.create";
    public const string EnrollmentActivateAction = "fabric.discovery.enrollment.activate";
    public const string CredentialRotateAction = "fabric.discovery.credential.rotate";
    public const string EnrollmentSuspendAction = "fabric.discovery.enrollment.suspend";
    public const string EnrollmentRevokeAction = "fabric.discovery.enrollment.revoke";
    public const string IngestionAcceptAction = "atlas.discovery.ingestion.accept";
    public const string IngestionDenyAction = "atlas.discovery.ingestion.deny";

    public static AuditResource EnrollmentResource(string enrollmentId) =>
        new($"fabric:discovery/enrollments/{enrollmentId}", "discovery-enrollment");

    public static AuditResource IngestionResource(string tenantId, string enrollmentId) =>
        new($"atlas:discovery/ingestion/{tenantId}/{enrollmentId}", "discovery-ingestion");
}
