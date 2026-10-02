using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Vev.Fabric.Contracts.Audit;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Sharing;

/// <summary>
/// Canonical lifecycle states for a data-sharing enrollment: the tenant-bound credential a source product uses to
/// push a minimized summary to a consuming product the customer has consented to.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<DataSharingEnrollmentState>))]
public enum DataSharingEnrollmentState
{
    Pending,
    Active,
    Suspended,
    Revoked,
    Expired
}

/// <summary>Explicit lifecycle transitions Fabric may apply to a data-sharing enrollment.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DataSharingEnrollmentTransition>))]
public enum DataSharingEnrollmentTransition
{
    /// <summary>Activate a pending enrollment by redeeming its one-time activation code.</summary>
    Activate,
    RotateCredential,
    Suspend,
    /// <summary>Revoke the enrollment. Either side may revoke; it takes effect at once and cannot be undone.</summary>
    Revoke
}

/// <summary>Which side of a data-sharing enrollment acted.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DataSharingParty>))]
public enum DataSharingParty
{
    /// <summary>The product that holds the data and pushes it (the customer's side).</summary>
    Source,
    /// <summary>The product that receives the push.</summary>
    Consumer
}

/// <summary>Lifecycle event types emitted around data-sharing enrollment and denied pushes.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<DataSharingLifecycleEventType>))]
public enum DataSharingLifecycleEventType
{
    EnrollmentCreated,
    EnrollmentActivated,
    CredentialRotated,
    PushDenied,
    EnrollmentSuspended,
    EnrollmentRevoked,
    CredentialExpired
}

/// <summary>
/// What one enrollment binds: exactly one source tenant to exactly one consumer-side account. The consumer decides the
/// binding when it creates the enrollment. A push never chooses it: nothing in a payload can move data to another
/// tenant or account.
/// </summary>
public sealed record DataSharingBinding(TenantContext SourceTenant, string ConsumerAccountId);

/// <summary>Timeline of a data-sharing enrollment.</summary>
public sealed record DataSharingEnrollmentTimeline(
    DateTimeOffset EnrolledAt,
    DateTimeOffset? CredentialExpiresAt = null,
    DateTimeOffset? ActivatedAt = null,
    DateTimeOffset? LastRotatedAt = null,
    DateTimeOffset? SuspendedAt = null,
    DateTimeOffset? RevokedAt = null,
    DataSharingParty? RevokedBy = null);

/// <summary>
/// The one-time activation code of an enrollment, as it is stored: a hash, never the code. The consumer's admin is shown
/// the code once; the source side enters it to activate. It is single use and it expires.
/// </summary>
public sealed record DataSharingActivationRecord(
    string EnrollmentId,
    string CodeHash,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    DateTimeOffset? ConsumedAt = null);

public sealed record DataSharingEnrollmentQuery(
    string EnrollmentId,
    DataSharingBinding Binding,
    PrincipalContext Principal,
    CapabilityId Capability,
    DateTimeOffset? AsOf = null);

/// <summary>The current state of a data-sharing enrollment.</summary>
public sealed record DataSharingEnrollmentStatus(
    string EnrollmentId,
    DataSharingBinding Binding,
    PrincipalContext Principal,
    CapabilityId Capability,
    DataSharingEnrollmentState State,
    string ReasonCode,
    DateTimeOffset EvaluatedAt,
    DataSharingEnrollmentTimeline Timeline);

/// <summary>
/// Apply one explicit transition. <c>Activate</c> must carry the stored <see cref="Activation"/> record and the code that
/// was presented; <c>Revoke</c> must say which side revokes.
/// </summary>
public sealed record DataSharingEnrollmentTransitionRequest(
    string EnrollmentId,
    DataSharingBinding Binding,
    PrincipalContext Principal,
    CapabilityId Capability,
    DataSharingEnrollmentTransition Transition,
    DateTimeOffset OccurredAt,
    DataSharingEnrollmentTimeline Timeline,
    DateTimeOffset? CredentialExpiresAt = null,
    DataSharingParty? RequestedBy = null,
    DataSharingActivationRecord? Activation = null,
    string? PresentedActivationCode = null);

/// <summary>Result of one transition. After a successful activation <see cref="Activation"/> is the consumed record to store.</summary>
public sealed record DataSharingEnrollmentTransitionResult(
    bool Accepted,
    string ReasonCode,
    DataSharingEnrollmentStatus Enrollment,
    DataSharingActivationRecord? Activation = null);

/// <summary>Public lifecycle event emitted around data-sharing enrollment and denied pushes.</summary>
public sealed record DataSharingLifecycleEvent(
    string EventId,
    DateTimeOffset OccurredAt,
    DataSharingBinding Binding,
    string EnrollmentId,
    string PrincipalId,
    string Source,
    DataSharingLifecycleEventType EventType,
    string ReasonCode,
    CapabilityId Capability,
    string CorrelationId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    IReadOnlyDictionary<string, string>? Metadata = null);

/// <summary>Outcome of redeeming an activation code.</summary>
public enum DataSharingActivationOutcome
{
    Accepted,
    Invalid,
    Expired,
    AlreadyUsed
}

public sealed record DataSharingActivationResult(DataSharingActivationOutcome Outcome, DataSharingActivationRecord Record)
{
    public bool Accepted => Outcome == DataSharingActivationOutcome.Accepted;
}

/// <summary>
/// Issue and redeem the one-time activation code. The code has 120 bits of entropy, is stored only as a hash bound to its
/// enrollment, compares in constant time, works once, and expires. Limiting guesses against a pending enrollment is the
/// service's job: it should suspend or revoke an enrollment after repeated invalid codes.
/// </summary>
public static class DataSharingActivationCodes
{
    public const int MaxLifetimeDays = 7;
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford base32
    private const int CodeBytes = 15; // 120 bits, 24 characters

    /// <summary>Issues a code. Show the returned code once and store only the record.</summary>
    public static (string Code, DataSharingActivationRecord Record) Issue(
        string enrollmentId, DateTimeOffset issuedAt, TimeSpan lifetime, Func<byte[]>? randomBytes = null)
    {
        if (string.IsNullOrWhiteSpace(enrollmentId)) throw new ArgumentException("An activation code needs an enrollment id.", nameof(enrollmentId));
        if (lifetime <= TimeSpan.Zero || lifetime > TimeSpan.FromDays(MaxLifetimeDays))
            throw new ArgumentOutOfRangeException(nameof(lifetime), $"An activation code must live longer than zero and at most {MaxLifetimeDays} days.");

        var bytes = (randomBytes ?? (() => RandomNumberGenerator.GetBytes(CodeBytes)))();
        if (bytes.Length != CodeBytes) throw new ArgumentException($"Expected {CodeBytes} random bytes.", nameof(randomBytes));
        var code = Format(Encode(bytes));
        return (code, new DataSharingActivationRecord(enrollmentId, Hash(enrollmentId, code), issuedAt, issuedAt + lifetime));
    }

    /// <summary>
    /// Redeems a presented code against the stored record. A wrong code is <c>Invalid</c> whatever the record's state, so a
    /// guess learns nothing about it; a right code on a used record is <c>AlreadyUsed</c>, on an old one <c>Expired</c>.
    /// </summary>
    public static DataSharingActivationResult Redeem(DataSharingActivationRecord record, string enrollmentId, string? presentedCode, DateTimeOffset asOf)
    {
        ArgumentNullException.ThrowIfNull(record);
        if (string.IsNullOrWhiteSpace(presentedCode) || !string.Equals(record.EnrollmentId, enrollmentId, StringComparison.Ordinal)
            || !FixedTimeEquals(Hash(enrollmentId, presentedCode), record.CodeHash))
            return new DataSharingActivationResult(DataSharingActivationOutcome.Invalid, record);
        if (record.ConsumedAt is not null) return new DataSharingActivationResult(DataSharingActivationOutcome.AlreadyUsed, record);
        if (asOf >= record.ExpiresAt) return new DataSharingActivationResult(DataSharingActivationOutcome.Expired, record);
        return new DataSharingActivationResult(DataSharingActivationOutcome.Accepted, record with { ConsumedAt = asOf });
    }

    /// <summary>Case, dashes and spaces do not matter, and the look-alike letters I, L and O read as 1, 1 and 0.</summary>
    public static string Normalize(string code)
    {
        var builder = new StringBuilder(code.Length);
        foreach (var c in code.ToUpperInvariant())
        {
            if (c is '-' or ' ') continue;
            builder.Append(c switch { 'I' or 'L' => '1', 'O' => '0', _ => c });
        }

        return builder.ToString();
    }

    public static string Hash(string enrollmentId, string code) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{enrollmentId}\n{Normalize(code)}")));

    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(a), Encoding.UTF8.GetBytes(b));

    private static string Encode(byte[] bytes)
    {
        var builder = new StringBuilder();
        int buffer = 0, bits = 0;
        foreach (var b in bytes)
        {
            buffer = (buffer << 8) | b;
            bits += 8;
            while (bits >= 5)
            {
                builder.Append(Alphabet[(buffer >> (bits - 5)) & 31]);
                bits -= 5;
            }
        }

        return builder.ToString();
    }

    private static string Format(string code) => string.Join('-', Enumerable.Range(0, code.Length / 4).Select(i => code.Substring(i * 4, 4)));
}

/// <summary>
/// Shared evaluation and transition validation for a data-sharing enrollment. Fail-static, like discovery enrollment: an
/// enrollment that is not active, or whose credential has expired, denies; it is never downgraded.
/// </summary>
public static class DataSharingEnrollmentStateMachine
{
    public static DataSharingEnrollmentStatus Evaluate(DataSharingEnrollmentQuery query, DataSharingEnrollmentTimeline timeline) =>
        Evaluate(query.EnrollmentId, query.Binding, query.Principal, query.Capability, timeline, query.AsOf ?? DateTimeOffset.UtcNow);

    public static DataSharingEnrollmentStatus Evaluate(
        string enrollmentId,
        DataSharingBinding binding,
        PrincipalContext principal,
        CapabilityId capability,
        DataSharingEnrollmentTimeline timeline,
        DateTimeOffset asOf)
    {
        ValidateTimeline(timeline);
        DataSharingEnrollmentStatus Status(DataSharingEnrollmentState state, string reason) =>
            new(enrollmentId, binding, principal, capability, state, reason, asOf, timeline);

        return timeline switch
        {
            { RevokedAt: not null } when asOf >= timeline.RevokedAt.Value => Status(DataSharingEnrollmentState.Revoked, ReasonCodes.SharingEnrollmentRevoked),
            { SuspendedAt: not null } when asOf >= timeline.SuspendedAt.Value => Status(DataSharingEnrollmentState.Suspended, ReasonCodes.SharingEnrollmentSuspended),
            { CredentialExpiresAt: not null } when asOf >= timeline.CredentialExpiresAt.Value => Status(DataSharingEnrollmentState.Expired, ReasonCodes.SharingCredentialExpired),
            { ActivatedAt: not null } when asOf >= timeline.ActivatedAt.Value => Status(DataSharingEnrollmentState.Active, ReasonCodes.Allow),
            _ => Status(DataSharingEnrollmentState.Pending, ReasonCodes.SharingEnrollmentPending)
        };
    }

    public static DataSharingEnrollmentTransitionResult Apply(DataSharingEnrollmentTransitionRequest request)
    {
        var current = Evaluate(request.EnrollmentId, request.Binding, request.Principal, request.Capability, request.Timeline, request.OccurredAt);
        var timeline = request.Timeline;
        DataSharingActivationRecord? activation = null;

        switch (request.Transition)
        {
            case DataSharingEnrollmentTransition.Activate:
                if (current.State != DataSharingEnrollmentState.Pending || request.CredentialExpiresAt is null || request.CredentialExpiresAt <= request.OccurredAt)
                {
                    return Reject(current, ReasonCodes.SharingLifecycleTransitionInvalid);
                }

                // The code is redeemed here, so an activation cannot be applied without having used it.
                if (request.Activation is null) return Reject(current, ReasonCodes.SharingActivationCodeInvalid);
                var redeemed = DataSharingActivationCodes.Redeem(request.Activation, request.EnrollmentId, request.PresentedActivationCode, request.OccurredAt);
                if (!redeemed.Accepted)
                {
                    return Reject(current, redeemed.Outcome switch
                    {
                        DataSharingActivationOutcome.Expired => ReasonCodes.SharingActivationCodeExpired,
                        DataSharingActivationOutcome.AlreadyUsed => ReasonCodes.SharingActivationCodeUsed,
                        _ => ReasonCodes.SharingActivationCodeInvalid
                    });
                }

                activation = redeemed.Record;
                timeline = request.Timeline with { ActivatedAt = request.OccurredAt, CredentialExpiresAt = request.CredentialExpiresAt };
                break;

            case DataSharingEnrollmentTransition.RotateCredential:
                if (current.State != DataSharingEnrollmentState.Active || request.CredentialExpiresAt is null || request.CredentialExpiresAt <= request.OccurredAt)
                {
                    return Reject(current, ReasonCodes.SharingLifecycleTransitionInvalid);
                }

                timeline = request.Timeline with { CredentialExpiresAt = request.CredentialExpiresAt, LastRotatedAt = request.OccurredAt };
                break;

            case DataSharingEnrollmentTransition.Suspend:
                if (current.State != DataSharingEnrollmentState.Active) return Reject(current, ReasonCodes.SharingLifecycleTransitionInvalid);
                timeline = request.Timeline with { SuspendedAt = request.OccurredAt };
                break;

            case DataSharingEnrollmentTransition.Revoke:
                // Either side may revoke, and it denies at once. The record says which side did.
                if (current.State == DataSharingEnrollmentState.Revoked || request.RequestedBy is null)
                {
                    return Reject(current, ReasonCodes.SharingLifecycleTransitionInvalid);
                }

                timeline = request.Timeline with { RevokedAt = request.OccurredAt, RevokedBy = request.RequestedBy };
                break;

            default:
                return Reject(current, ReasonCodes.SharingLifecycleTransitionInvalid);
        }

        var resulting = Evaluate(request.EnrollmentId, request.Binding, request.Principal, request.Capability, timeline, request.OccurredAt);
        return new DataSharingEnrollmentTransitionResult(true, resulting.ReasonCode, resulting, activation);
    }

    private static DataSharingEnrollmentTransitionResult Reject(DataSharingEnrollmentStatus current, string reasonCode) => new(false, reasonCode, current);

    private static void ValidateTimeline(DataSharingEnrollmentTimeline timeline)
    {
        if (timeline.CredentialExpiresAt is not null && timeline.CredentialExpiresAt < timeline.EnrolledAt)
            throw new ArgumentException("credentialExpiresAt must be on or after enrolledAt.");
        if (timeline.ActivatedAt is not null && timeline.ActivatedAt < timeline.EnrolledAt)
            throw new ArgumentException("activatedAt must be on or after enrolledAt.");
        if (timeline.LastRotatedAt is not null && timeline.LastRotatedAt < timeline.EnrolledAt)
            throw new ArgumentException("lastRotatedAt must be on or after enrolledAt.");
        if (timeline.SuspendedAt is not null && timeline.ActivatedAt is null)
            throw new ArgumentException("suspendedAt requires an activated enrollment.");
        if (timeline.SuspendedAt is not null && timeline.SuspendedAt < timeline.ActivatedAt)
            throw new ArgumentException("suspendedAt must be on or after activatedAt.");
        if (timeline.RevokedAt is not null && timeline.RevokedAt < timeline.EnrolledAt)
            throw new ArgumentException("revokedAt must be on or after enrolledAt.");
        if ((timeline.RevokedAt is null) != (timeline.RevokedBy is null))
            throw new ArgumentException("revokedAt and revokedBy must be set together.");
    }
}

/// <summary>Shared action and resource vocabulary for data-sharing audit events through the Fabric <see cref="AuditEvent"/> envelope.</summary>
public static class DataSharingAuditVocabulary
{
    public const string EnrollmentCreateAction = "fabric.sharing.enrollment.create";
    public const string EnrollmentActivateAction = "fabric.sharing.enrollment.activate";
    public const string CredentialRotateAction = "fabric.sharing.credential.rotate";
    public const string EnrollmentSuspendAction = "fabric.sharing.enrollment.suspend";
    public const string EnrollmentRevokeAction = "fabric.sharing.enrollment.revoke";
    public const string PushAcceptAction = "atlas.landscape.share.push.accept";
    public const string PushDenyAction = "atlas.landscape.share.push.deny";

    public static AuditResource EnrollmentResource(string enrollmentId) =>
        new($"fabric:sharing/enrollments/{enrollmentId}", "data-sharing-enrollment");

    public static AuditResource PushResource(string tenantId, string enrollmentId) =>
        new($"atlas:landscape/share/push/{tenantId}/{enrollmentId}", "data-sharing-push");
}
