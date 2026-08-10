using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Vev.Fabric.Contracts;
using Vev.Fabric.Contracts.Entitlements;

namespace Vev.Fabric.Contracts.Tests;

public sealed class LocalEntitlementEvaluatorTests
{
    private static readonly byte[] SharedKey = Encoding.UTF8.GetBytes("vev-test-signing-key-32-bytes-ish");
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void Evaluate_Allows_GrantedCapability_FromFreshSnapshot()
    {
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var evaluator = CreateEvaluator(now);
        evaluator.LoadSnapshot(CreateSnapshotDocument(CreateSnapshot(now)));

        var decision = evaluator.Evaluate(CreateRequest("atlas.catalogue.write"));

        Assert.True(decision.Allowed);
        Assert.Equal(ReasonCodes.EntitlementGranted, decision.ReasonCode);
        Assert.Equal(5m, decision.Limits?["atlas.entities.max"]);
    }

    [Fact]
    public void Evaluate_Denies_UngrantedCapability_FromVerifiedSnapshot()
    {
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var evaluator = CreateEvaluator(now);
        evaluator.LoadSnapshot(CreateSnapshotDocument(CreateSnapshot(now)));

        var decision = evaluator.Evaluate(CreateRequest("atlas.analysis.eol"));

        Assert.False(decision.Allowed);
        Assert.Equal(ReasonCodes.EntitlementDenied, decision.ReasonCode);
    }

    [Fact]
    public void Evaluate_RemainsFailStatic_WithinGrace_WhenSnapshotHasExpired()
    {
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var evaluator = CreateEvaluator(now.AddDays(5));
        evaluator.LoadSnapshot(CreateSnapshotDocument(CreateSnapshot(now)));

        var decision = evaluator.Evaluate(CreateRequest("atlas.catalogue.write"));

        Assert.True(decision.Allowed);
        Assert.Contains(":grace", decision.Source, StringComparison.Ordinal);
    }

    [Fact]
    public void Evaluate_Denies_WhenSnapshotGraceHasExpired()
    {
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var evaluator = CreateEvaluator(now.AddDays(9));
        evaluator.LoadSnapshot(CreateSnapshotDocument(CreateSnapshot(now)));

        var decision = evaluator.Evaluate(CreateRequest("atlas.catalogue.write"));

        Assert.False(decision.Allowed);
        Assert.Equal(ReasonCodes.EntitlementSnapshotStale, decision.ReasonCode);
    }

    [Fact]
    public void LoadSnapshot_Rejects_InvalidSignature_AndPreservesLastGoodSnapshot()
    {
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var evaluator = CreateEvaluator(now);
        evaluator.LoadSnapshot(CreateSnapshotDocument(CreateSnapshot(now)));

        var invalid = CreateSnapshotDocument(CreateSnapshot(now), signatureOverride: Convert.ToBase64String([1, 2, 3]));
        var result = evaluator.LoadSnapshot(invalid);
        var decision = evaluator.Evaluate(CreateRequest("atlas.catalogue.write"));

        Assert.False(result.IsValid);
        Assert.Equal(ReasonCodes.EntitlementSnapshotInvalid, result.ReasonCode);
        Assert.True(decision.Allowed);
    }

    [Fact]
    public void Evaluate_Denies_WhenSnapshotBelongsToDifferentTenant()
    {
        var now = new DateTimeOffset(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);
        var evaluator = CreateEvaluator(now);
        evaluator.LoadSnapshot(CreateSnapshotDocument(CreateSnapshot(now) with { Tenant = "tenant-b" }));

        var decision = evaluator.Evaluate(CreateRequest("atlas.catalogue.write"));

        Assert.False(decision.Allowed);
        Assert.Equal(ReasonCodes.EntitlementSnapshotTenantMismatch, decision.ReasonCode);
    }

    private static LocalEntitlementEvaluator CreateEvaluator(DateTimeOffset now)
    {
        var verifier = new JsonSignedEntitlementSnapshotVerifier(
            new HmacSha256SignatureVerifier(new Dictionary<string, byte[]>
            {
                ["dev-key"] = SharedKey
            }));

        return new LocalEntitlementEvaluator(verifier, new ManualTimeProvider(now));
    }

    private static EntitlementSnapshot CreateSnapshot(DateTimeOffset issuedAt) =>
        new(
            "tenant-a",
            issuedAt,
            issuedAt.AddDays(3),
            issuedAt.AddDays(7),
            [
                new EntitlementGrant(
                    "atlas.catalogue.write",
                    "subscription",
                    new Dictionary<string, decimal> { ["atlas.entities.max"] = 5m })
            ]);

    private static SignedEntitlementSnapshot CreateSnapshotDocument(
        EntitlementSnapshot snapshot,
        string? signatureOverride = null)
    {
        var payloadJson = JsonSerializer.Serialize(snapshot, SerializerOptions);
        var signature = signatureOverride ?? Sign(payloadJson);
        return new SignedEntitlementSnapshot("dev-key", "HS256", payloadJson, signature);
    }

    private static string Sign(string payloadJson)
    {
        using var hmac = new HMACSHA256(SharedKey);
        return Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(payloadJson)));
    }

    private static EntitlementRequest CreateRequest(string capability) =>
        new(
            new TenantContext("tenant-a"),
            new CapabilityId(capability),
            new PrincipalContext("principal-1", "Test User", Array.Empty<string>()));

    private sealed class ManualTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
