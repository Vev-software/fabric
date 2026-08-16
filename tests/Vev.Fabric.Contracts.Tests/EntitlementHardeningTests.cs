using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vev.Fabric.Contracts;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Tests;

/// <summary>
/// Offline evaluator hardening (fabric#9, security#1 T3/T4/T7): anti-rollback, clock-regression and
/// trial hard-stop, plus the purchased freeze-open-within-grace guarantee that must survive.
/// </summary>
public sealed class EntitlementHardeningTests
{
    private const string KeyId = "test-key";
    private const string Capability = "atlas.catalogue.write";
    private static readonly byte[] Key = Encoding.UTF8.GetBytes("vev-test-signing-key-32-bytes-ish!");

    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private sealed class MutableTimeProvider(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static SignedEntitlementSnapshot Sign(EntitlementSnapshot payload)
    {
        var payloadJson = JsonSerializer.Serialize(payload, Json);
        using var hmac = new HMACSHA256(Key);
        var signature = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson)));
        return new SignedEntitlementSnapshot(KeyId, "HS256", payloadJson, signature);
    }

    private static LocalEntitlementEvaluator NewEvaluator(TimeProvider time)
    {
        var verifier = new JsonSignedEntitlementSnapshotVerifier(
            new HmacSha256SignatureVerifier(new Dictionary<string, byte[]> { [KeyId] = Key }));
        return new LocalEntitlementEvaluator(verifier, time);
    }

    private static EntitlementSnapshot Snapshot(
        DateTimeOffset issuedAt, DateTimeOffset expiresAt, DateTimeOffset graceUntil, string source, long? counter = null) =>
        new("tenant-a", issuedAt, expiresAt, graceUntil, [new EntitlementGrant(Capability, source)], counter);

    private static EntitlementRequest Request() =>
        new(new TenantContext("tenant-a"), new CapabilityId(Capability), new PrincipalContext("p1", null, []));

    private static readonly DateTimeOffset T0 = new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    // ---- T3 anti-rollback ----

    [Fact]
    public void Rollback_to_an_older_snapshot_is_rejected()
    {
        var evaluator = NewEvaluator(new MutableTimeProvider(T0));
        Assert.True(evaluator.LoadSnapshot(Sign(Snapshot(T0, T0.AddDays(30), T0.AddDays(60), "subscription"))).IsValid);

        var older = evaluator.LoadSnapshot(Sign(Snapshot(T0.AddDays(-1), T0.AddDays(30), T0.AddDays(60), "subscription")));

        Assert.False(older.IsValid);
        Assert.Equal(ReasonCodes.EntitlementSnapshotRolledBack, older.ReasonCode);
        Assert.Equal(T0, evaluator.HighestObservedIssuedAt);
    }

    [Fact]
    public void Equal_issuedAt_is_accepted_idempotently()
    {
        var evaluator = NewEvaluator(new MutableTimeProvider(T0));
        Assert.True(evaluator.LoadSnapshot(Sign(Snapshot(T0, T0.AddDays(30), T0.AddDays(60), "subscription"))).IsValid);
        Assert.True(evaluator.LoadSnapshot(Sign(Snapshot(T0, T0.AddDays(30), T0.AddDays(60), "subscription"))).IsValid);
    }

    [Fact]
    public void A_lower_counter_is_rejected_even_with_a_newer_timestamp()
    {
        var evaluator = NewEvaluator(new MutableTimeProvider(T0));
        Assert.True(evaluator.LoadSnapshot(Sign(Snapshot(T0, T0.AddDays(30), T0.AddDays(60), "subscription", counter: 5))).IsValid);

        var replay = evaluator.LoadSnapshot(Sign(Snapshot(T0.AddDays(1), T0.AddDays(30), T0.AddDays(60), "subscription", counter: 4)));

        Assert.False(replay.IsValid);
        Assert.Equal(ReasonCodes.EntitlementSnapshotRolledBack, replay.ReasonCode);
    }

    // ---- T4 clock regression ----

    [Fact]
    public void A_clock_earlier_than_last_observed_time_is_denied()
    {
        var time = new MutableTimeProvider(T0);
        var evaluator = NewEvaluator(time);
        evaluator.LoadSnapshot(Sign(Snapshot(T0, T0.AddDays(30), T0.AddDays(60), "subscription")));

        time.Now = T0.AddDays(-2); // roll the wall clock back below the snapshot's issuedAt floor
        var decision = evaluator.Evaluate(Request());

        Assert.False(decision.Allowed);
        Assert.Equal(ReasonCodes.EntitlementClockRegression, decision.ReasonCode);
    }

    // ---- T7 trial hard-stop vs purchased freeze-open ----

    [Fact]
    public void Trial_denies_on_expiry_even_within_the_grace_window()
    {
        var time = new MutableTimeProvider(T0.AddDays(10)); // past ExpiresAt, before GraceUntil
        var evaluator = NewEvaluator(time);
        evaluator.LoadSnapshot(Sign(Snapshot(T0, T0.AddDays(5), T0.AddDays(30), "trial")));

        var decision = evaluator.Evaluate(Request());

        Assert.False(decision.Allowed);
        Assert.Equal(ReasonCodes.TrialExpired, decision.ReasonCode);
    }

    [Fact]
    public void Purchased_freezes_open_within_grace_after_expiry()
    {
        var time = new MutableTimeProvider(T0.AddDays(10)); // past ExpiresAt, before GraceUntil
        var evaluator = NewEvaluator(time);
        evaluator.LoadSnapshot(Sign(Snapshot(T0, T0.AddDays(5), T0.AddDays(30), "subscription")));

        var decision = evaluator.Evaluate(Request());

        Assert.True(decision.Allowed);
        Assert.Contains(":grace", decision.Source, StringComparison.Ordinal);
    }
}
