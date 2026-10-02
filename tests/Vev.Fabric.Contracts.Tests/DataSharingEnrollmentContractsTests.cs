using System.Text.Json;
using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Sharing;

namespace Vev.Fabric.Contracts.Tests;

public sealed class DataSharingEnrollmentContractsTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly DateTimeOffset EnrolledAt = new(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
    private static readonly DataSharingBinding Binding = new(new TenantContext("tenant-a"), "account-42");
    private static readonly CapabilityId Capability = new("atlas.landscape.share");
    private const string EnrollmentId = "share-1";

    private static PrincipalContext Principal() => new("atlas-pusher-1", "Atlas landscape sharing", ["Machine"]);

    private static (string Code, DataSharingActivationRecord Record) Issue(TimeSpan? lifetime = null) =>
        DataSharingActivationCodes.Issue(EnrollmentId, EnrolledAt, lifetime ?? TimeSpan.FromMinutes(30));

    private static DataSharingEnrollmentTransitionRequest Request(DataSharingEnrollmentTransition transition, DataSharingEnrollmentTimeline timeline,
        DateTimeOffset? at = null, DateTimeOffset? credentialExpiresAt = null, DataSharingParty? by = null,
        DataSharingActivationRecord? activation = null, string? code = null) =>
        new(EnrollmentId, Binding, Principal(), Capability, transition, at ?? EnrolledAt, timeline, credentialExpiresAt, by, activation, code);

    private static DataSharingEnrollmentTimeline ActiveTimeline() => new(EnrolledAt, CredentialExpiresAt: EnrolledAt.AddDays(30), ActivatedAt: EnrolledAt);

    // ---- State -------------------------------------------------------------------------------------------

    [Fact]
    public void A_new_enrollment_is_pending_and_the_binding_is_carried_through()
    {
        var status = DataSharingEnrollmentStateMachine.Evaluate(EnrollmentId, Binding, Principal(), Capability, new DataSharingEnrollmentTimeline(EnrolledAt), EnrolledAt);

        Assert.Equal((DataSharingEnrollmentState.Pending, ReasonCodes.SharingEnrollmentPending), (status.State, status.ReasonCode));
        Assert.Equal(Binding, status.Binding);
    }

    [Theory]
    [InlineData(0, DataSharingEnrollmentState.Active)]
    [InlineData(29, DataSharingEnrollmentState.Active)]
    [InlineData(30, DataSharingEnrollmentState.Expired)]
    public void An_active_credential_expires_at_its_expiry_and_expiry_is_not_a_recovery_state(int days, DataSharingEnrollmentState expected)
    {
        var status = DataSharingEnrollmentStateMachine.Evaluate(EnrollmentId, Binding, Principal(), Capability, ActiveTimeline(), EnrolledAt.AddDays(days));

        Assert.Equal(expected, status.State);
    }

    [Fact]
    public void An_inconsistent_timeline_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => DataSharingEnrollmentStateMachine.Evaluate(EnrollmentId, Binding, Principal(), Capability,
            new DataSharingEnrollmentTimeline(EnrolledAt, SuspendedAt: EnrolledAt), EnrolledAt));
        Assert.Throws<ArgumentException>(() => DataSharingEnrollmentStateMachine.Evaluate(EnrollmentId, Binding, Principal(), Capability,
            new DataSharingEnrollmentTimeline(EnrolledAt, RevokedAt: EnrolledAt), EnrolledAt));   // revokedBy missing
        Assert.Throws<ArgumentException>(() => DataSharingEnrollmentStateMachine.Evaluate(EnrollmentId, Binding, Principal(), Capability,
            new DataSharingEnrollmentTimeline(EnrolledAt, RevokedBy: DataSharingParty.Source), EnrolledAt));   // revokedAt missing
    }

    // ---- Activation with the one-time code ---------------------------------------------------------------

    [Fact]
    public void Activate_with_the_right_code_activates_and_returns_the_consumed_record()
    {
        var (code, record) = Issue();
        var at = EnrolledAt.AddMinutes(10);

        var result = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Activate, new DataSharingEnrollmentTimeline(EnrolledAt),
            at, at.AddDays(30), activation: record, code: code));

        Assert.True(result.Accepted);
        Assert.Equal((DataSharingEnrollmentState.Active, ReasonCodes.Allow), (result.Enrollment.State, result.ReasonCode));
        Assert.Equal(at, result.Activation!.ConsumedAt);
        Assert.Equal(at, result.Enrollment.Timeline.ActivatedAt);
    }

    [Fact]
    public void The_code_works_once_a_second_activation_with_it_is_refused_as_used()
    {
        var (code, record) = Issue();
        var at = EnrolledAt.AddMinutes(10);
        var first = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Activate, new DataSharingEnrollmentTimeline(EnrolledAt),
            at, at.AddDays(30), activation: record, code: code));

        // Replayed against a fresh pending timeline with the stored, now consumed, record.
        var replay = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Activate, new DataSharingEnrollmentTimeline(EnrolledAt),
            at.AddMinutes(1), at.AddDays(30), activation: first.Activation, code: code));

        Assert.False(replay.Accepted);
        Assert.Equal(ReasonCodes.SharingActivationCodeUsed, replay.ReasonCode);
        Assert.Equal(DataSharingEnrollmentState.Pending, replay.Enrollment.State);
    }

    [Fact]
    public void An_expired_code_is_refused()
    {
        var (code, record) = Issue(TimeSpan.FromMinutes(30));
        var at = EnrolledAt.AddMinutes(30);   // at the expiry instant

        var result = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Activate, new DataSharingEnrollmentTimeline(EnrolledAt),
            at, at.AddDays(30), activation: record, code: code));

        Assert.False(result.Accepted);
        Assert.Equal(ReasonCodes.SharingActivationCodeExpired, result.ReasonCode);
    }

    [Theory]
    [InlineData("WRONG-CODE")]
    [InlineData("")]
    [InlineData(null)]
    public void A_wrong_or_missing_code_is_refused(string? presented)
    {
        var (_, record) = Issue();

        var result = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Activate, new DataSharingEnrollmentTimeline(EnrolledAt),
            EnrolledAt.AddMinutes(1), EnrolledAt.AddDays(30), activation: record, code: presented));

        Assert.False(result.Accepted);
        Assert.Equal(ReasonCodes.SharingActivationCodeInvalid, result.ReasonCode);
        Assert.Null(result.Activation);
    }

    [Fact]
    public void Activate_without_a_stored_record_a_credential_expiry_or_from_another_state_is_refused()
    {
        var (code, record) = Issue();
        var pending = new DataSharingEnrollmentTimeline(EnrolledAt);

        var noRecord = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Activate, pending, EnrolledAt, EnrolledAt.AddDays(30), code: code));
        var noExpiry = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Activate, pending, EnrolledAt, null, activation: record, code: code));
        var alreadyActive = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Activate, ActiveTimeline(), EnrolledAt.AddDays(1), EnrolledAt.AddDays(60), activation: record, code: code));

        Assert.Equal(ReasonCodes.SharingActivationCodeInvalid, noRecord.ReasonCode);
        Assert.Equal(ReasonCodes.SharingLifecycleTransitionInvalid, noExpiry.ReasonCode);
        Assert.Equal(ReasonCodes.SharingLifecycleTransitionInvalid, alreadyActive.ReasonCode);
        Assert.All(new[] { noRecord, noExpiry, alreadyActive }, r => Assert.False(r.Accepted));
    }

    [Fact]
    public void A_code_is_bound_to_its_enrollment()
    {
        var (code, record) = Issue();

        var other = DataSharingActivationCodes.Redeem(record, "share-2", code, EnrolledAt);

        Assert.Equal(DataSharingActivationOutcome.Invalid, other.Outcome);
        Assert.NotEqual(DataSharingActivationCodes.Hash("share-2", code), record.CodeHash);
    }

    [Fact]
    public void A_code_is_read_case_and_dash_insensitively_and_look_alike_letters_are_forgiven_but_only_the_hash_is_stored()
    {
        var (code, record) = Issue();
        var sloppy = code.ToLowerInvariant().Replace("-", " ").Replace('0', 'o').Replace('1', 'l');

        var redeemed = DataSharingActivationCodes.Redeem(record, EnrollmentId, sloppy, EnrolledAt.AddMinutes(1));

        Assert.True(redeemed.Accepted);
        Assert.DoesNotContain(code, JsonSerializer.Serialize(record, SerializerOptions));
        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{4}(-[0-9A-HJKMNP-TV-Z]{4}){5}$", code);
    }

    [Fact]
    public void Codes_are_random_and_long_lived_ones_are_refused()
    {
        var codes = Enumerable.Range(0, 50).Select(_ => Issue().Code).ToHashSet();

        Assert.Equal(50, codes.Count);
        Assert.Throws<ArgumentOutOfRangeException>(() => Issue(TimeSpan.FromDays(DataSharingActivationCodes.MaxLifetimeDays + 1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => Issue(TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => DataSharingActivationCodes.Issue(EnrollmentId, EnrolledAt, TimeSpan.FromHours(1), () => new byte[3]));
    }

    // ---- The other transitions ---------------------------------------------------------------------------

    [Fact]
    public void RotateCredential_updates_expiry_and_rotation_time_only_when_active()
    {
        var rotated = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.RotateCredential, ActiveTimeline(),
            EnrolledAt.AddDays(1), EnrolledAt.AddDays(60)));
        var pending = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.RotateCredential, new DataSharingEnrollmentTimeline(EnrolledAt),
            EnrolledAt, EnrolledAt.AddDays(60)));
        var inThePast = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.RotateCredential, ActiveTimeline(),
            EnrolledAt.AddDays(1), EnrolledAt.AddDays(1)));

        Assert.True(rotated.Accepted);
        Assert.Equal((EnrolledAt.AddDays(60), EnrolledAt.AddDays(1)), (rotated.Enrollment.Timeline.CredentialExpiresAt, rotated.Enrollment.Timeline.LastRotatedAt));
        Assert.Equal(ReasonCodes.SharingLifecycleTransitionInvalid, pending.ReasonCode);
        Assert.False(inThePast.Accepted);
    }

    [Fact]
    public void Suspend_applies_to_an_active_enrollment_and_there_is_no_resume()
    {
        var suspended = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Suspend, ActiveTimeline(), EnrolledAt.AddDays(1)));
        var again = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Suspend, suspended.Enrollment.Timeline, EnrolledAt.AddDays(2)));
        var rotateWhileSuspended = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.RotateCredential, suspended.Enrollment.Timeline,
            EnrolledAt.AddDays(2), EnrolledAt.AddDays(60)));

        Assert.Equal((true, DataSharingEnrollmentState.Suspended, ReasonCodes.SharingEnrollmentSuspended),
            (suspended.Accepted, suspended.Enrollment.State, suspended.ReasonCode));
        Assert.False(again.Accepted);
        Assert.False(rotateWhileSuspended.Accepted);
        Assert.Equal(DataSharingEnrollmentState.Suspended, rotateWhileSuspended.Enrollment.State);
    }

    // ---- Revocation from either side ----------------------------------------------------------------------

    [Theory]
    [InlineData(DataSharingParty.Source)]
    [InlineData(DataSharingParty.Consumer)]
    public void Either_side_can_revoke_and_the_record_says_which(DataSharingParty side)
    {
        var result = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Revoke, ActiveTimeline(), EnrolledAt.AddDays(1), by: side));

        Assert.True(result.Accepted);
        Assert.Equal((DataSharingEnrollmentState.Revoked, ReasonCodes.SharingEnrollmentRevoked, side),
            (result.Enrollment.State, result.ReasonCode, result.Enrollment.Timeline.RevokedBy));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void A_pending_or_suspended_enrollment_can_be_revoked_too(bool suspended)
    {
        var timeline = suspended ? ActiveTimeline() with { SuspendedAt = EnrolledAt.AddHours(1) } : new DataSharingEnrollmentTimeline(EnrolledAt);

        var result = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Revoke, timeline, EnrolledAt.AddDays(1), by: DataSharingParty.Consumer));

        Assert.True(result.Accepted);
        Assert.Equal(DataSharingEnrollmentState.Revoked, result.Enrollment.State);
    }

    [Fact]
    public void Revoke_needs_a_side_and_cannot_be_repeated_and_nothing_follows_a_revocation()
    {
        var noSide = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Revoke, ActiveTimeline(), EnrolledAt.AddDays(1)));
        var revoked = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Revoke, ActiveTimeline(), EnrolledAt.AddDays(1), by: DataSharingParty.Source));
        var again = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.Revoke, revoked.Enrollment.Timeline, EnrolledAt.AddDays(2), by: DataSharingParty.Consumer));
        var rotate = DataSharingEnrollmentStateMachine.Apply(Request(DataSharingEnrollmentTransition.RotateCredential, revoked.Enrollment.Timeline,
            EnrolledAt.AddDays(2), EnrolledAt.AddDays(60)));

        Assert.False(noSide.Accepted);
        Assert.Equal(ReasonCodes.SharingLifecycleTransitionInvalid, noSide.ReasonCode);
        Assert.False(again.Accepted);
        Assert.Equal(DataSharingParty.Source, again.Enrollment.Timeline.RevokedBy);   // the first revocation stands
        Assert.False(rotate.Accepted);
    }

    // ---- Contract shape -----------------------------------------------------------------------------------

    [Fact]
    public void The_published_sample_activation_really_activates()
    {
        // The conformance sample carries a real code and its hash, so a consumer can replay the documented flow.
        var sample = JsonSerializer.Deserialize<DataSharingEnrollmentTransitionRequest>(
            File.ReadAllText(Path.Combine(TestSchemas.SampleDir, "data-sharing-enrollment-transition-request.sample.json")), SerializerOptions)!;

        var result = DataSharingEnrollmentStateMachine.Apply(sample);

        Assert.True(result.Accepted, result.ReasonCode);
        Assert.Equal(DataSharingEnrollmentState.Active, result.Enrollment.State);
        Assert.NotNull(result.Activation!.ConsumedAt);
    }

    [Fact]
    public void Audit_vocabulary_is_stable()
    {
        Assert.Equal("fabric.sharing.enrollment.create", DataSharingAuditVocabulary.EnrollmentCreateAction);
        Assert.Equal("fabric.sharing.enrollment.activate", DataSharingAuditVocabulary.EnrollmentActivateAction);
        Assert.Equal("fabric.sharing.credential.rotate", DataSharingAuditVocabulary.CredentialRotateAction);
        Assert.Equal("fabric.sharing.enrollment.suspend", DataSharingAuditVocabulary.EnrollmentSuspendAction);
        Assert.Equal("fabric.sharing.enrollment.revoke", DataSharingAuditVocabulary.EnrollmentRevokeAction);
        Assert.Equal("atlas.landscape.share.push.accept", DataSharingAuditVocabulary.PushAcceptAction);
        Assert.Equal("atlas.landscape.share.push.deny", DataSharingAuditVocabulary.PushDenyAction);
        Assert.Equal("fabric:sharing/enrollments/share-1", DataSharingAuditVocabulary.EnrollmentResource("share-1").Value);
        Assert.Equal("atlas:landscape/share/push/tenant-a/share-1", DataSharingAuditVocabulary.PushResource("tenant-a", "share-1").Value);
    }

    [Fact]
    public void The_reason_codes_are_in_the_taxonomy_and_deny()
    {
        var codes = new[]
        {
            ReasonCodes.SharingEnrollmentPending, ReasonCodes.SharingEnrollmentSuspended, ReasonCodes.SharingEnrollmentRevoked, ReasonCodes.SharingCredentialExpired,
            ReasonCodes.SharingLifecycleTransitionInvalid, ReasonCodes.SharingActivationCodeInvalid, ReasonCodes.SharingActivationCodeExpired,
            ReasonCodes.SharingActivationCodeUsed, ReasonCodes.SharingBindingMismatch,
        };

        Assert.All(codes, code => Assert.True(Vev.Fabric.Contracts.Taxonomy.Reasons.All.Single(r => r.Code == code).Deny, code));
    }
}
