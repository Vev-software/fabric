using System.Text.Json;
using System.Text.Json.Nodes;
using Vev.Fabric.Contracts.Entitlements;
using Vev.Fabric.Contracts.Membership;

namespace Vev.Fabric.Contracts.Tests;

public sealed class TenantMembershipContractsTests
{
    private const string Schema = "tenant-membership-snapshot.schema.json";
    private const string Issuer = "https://id.example.test/";
    private const string OtherIssuer = "https://other-id.example.test/";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly DateTimeOffset Issued = new(2026, 10, 1, 10, 0, 0, TimeSpan.Zero);

    private static TenantMembershipSnapshot Snapshot(
        long sequence = 7,
        string tenant = "tenant-a",
        int version = TenantMembershipSnapshot.CurrentSchemaVersion,
        TenantMember[]? members = null) =>
        new(
            version,
            new TenantContext(tenant),
            sequence,
            Issued,
            Issued.AddMinutes(5),
            members ??
            [
                new TenantMember(new TenantMemberIdentity(Issuer, "subject-1"), ["Reader"]),
                new TenantMember(new TenantMemberIdentity(OtherIssuer, "subject-1"), ["TenantOwner"])
            ]);

    private static (TenantMembershipRegistry Registry, ManualClock Clock) NewRegistry()
    {
        var clock = new ManualClock(Issued.AddSeconds(1));
        return (new TenantMembershipRegistry("tenant-a", clock), clock);
    }

    private static JsonNode? ReadFixture(string directory, string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, directory, file)));

    [Theory]
    [InlineData("tenant-membership-snapshot.sample.json")]
    [InlineData("tenant-membership-snapshot.other-tenant.sample.json")]
    public void Supported_samples_match_the_schema_and_deserialize(string file)
    {
        var instance = ReadFixture("samples", file);

        Assert.True(JsonSchemaTestHelpers.Evaluate(TestSchemas.Load(Schema), instance).IsValid);

        var snapshot = JsonSerializer.Deserialize<TenantMembershipSnapshot>(instance!.ToJsonString(), SerializerOptions);
        Assert.NotNull(snapshot);
        Assert.Equal(TenantMembershipSnapshot.CurrentSchemaVersion, snapshot!.SchemaVersion);
        Assert.Equal(3, snapshot.Members.Count);
    }

    [Theory]
    [InlineData("tenant-membership-snapshot.unknown-version.json")]
    [InlineData("tenant-membership-snapshot.missing-issuer.json")]
    [InlineData("tenant-membership-snapshot.empty-roles.json")]
    [InlineData("tenant-membership-snapshot.duplicate-role.json")]
    public void Invalid_fixtures_are_rejected_by_the_schema(string file)
    {
        var results = JsonSchemaTestHelpers.Evaluate(TestSchemas.Load(Schema), ReadFixture("invalid", file));

        Assert.False(results.IsValid);
    }

    [Fact]
    public void Dotnet_sdk_serializes_a_schema_valid_document()
    {
        var json = JsonSerializer.Serialize(Snapshot(), SerializerOptions);

        Assert.True(JsonSchemaTestHelpers.Evaluate(TestSchemas.Load(Schema), JsonNode.Parse(json)).IsValid);
    }

    [Fact]
    public void Nothing_is_a_member_before_a_snapshot_is_applied()
    {
        var (registry, _) = NewRegistry();

        var resolution = registry.Resolve(Issuer, "subject-1");

        Assert.False(resolution.IsMember);
        Assert.Equal(ReasonCodes.MembershipSnapshotUnavailable, resolution.ReasonCode);
    }

    [Fact]
    public void Applied_snapshot_resolves_exact_issuer_and_subject()
    {
        var (registry, _) = NewRegistry();
        Assert.True(registry.Apply(Snapshot()).Accepted);

        var member = registry.Resolve(Issuer, "subject-1");
        var sameSubjectOtherIssuer = registry.Resolve(OtherIssuer, "subject-1");

        Assert.Equal(["Reader"], member.Roles);
        Assert.Equal(["TenantOwner"], sameSubjectOtherIssuer.Roles);
        Assert.Equal(ReasonCodes.MembershipNotFound, registry.Resolve(Issuer, "SUBJECT-1").ReasonCode);
        Assert.Equal(ReasonCodes.MembershipNotFound, registry.Resolve("https://third.example.test/", "subject-1").ReasonCode);
    }

    [Fact]
    public void Identity_scope_is_issuer_qualified_so_one_subject_cannot_inherit_another_issuers_role()
    {
        var (registry, _) = NewRegistry();
        registry.Apply(Snapshot(members:
        [
            new TenantMember(new TenantMemberIdentity(Issuer, "subject-1"), ["Reader"])
        ]));

        Assert.False(registry.Resolve(OtherIssuer, "subject-1").IsMember);
    }

    [Fact]
    public void Newer_sequence_removes_a_member()
    {
        var (registry, _) = NewRegistry();
        registry.Apply(Snapshot(sequence: 7));

        var next = registry.Apply(Snapshot(sequence: 8, members:
        [
            new TenantMember(new TenantMemberIdentity(OtherIssuer, "subject-1"), ["TenantOwner"])
        ]));

        Assert.True(next.Accepted);
        Assert.False(registry.Resolve(Issuer, "subject-1").IsMember);
        Assert.True(registry.Resolve(OtherIssuer, "subject-1").IsMember);
    }

    [Fact]
    public void Unknown_schema_version_is_rejected_and_leaves_state_unchanged()
    {
        var (registry, _) = NewRegistry();
        registry.Apply(Snapshot());

        var result = registry.Apply(Snapshot(sequence: 9, version: 2));

        Assert.False(result.Accepted);
        Assert.Equal(ReasonCodes.MembershipSnapshotUnsupportedVersion, result.ReasonCode);
        Assert.True(registry.Resolve(Issuer, "subject-1").IsMember);
    }

    [Fact]
    public void Other_tenants_snapshot_is_rejected()
    {
        var (registry, _) = NewRegistry();

        var result = registry.Apply(Snapshot(tenant: "tenant-b"));

        Assert.Equal(ReasonCodes.MembershipSnapshotTenantMismatch, result.ReasonCode);
        Assert.False(registry.Resolve(Issuer, "subject-1").IsMember);
    }

    [Fact]
    public void Lower_sequence_is_rejected_as_rollback()
    {
        var (registry, _) = NewRegistry();
        registry.Apply(Snapshot(sequence: 7));

        var result = registry.Apply(Snapshot(sequence: 6));

        Assert.Equal(ReasonCodes.MembershipSnapshotRolledBack, result.ReasonCode);
    }

    [Fact]
    public void Equal_sequence_is_idempotent_only_for_identical_content()
    {
        var (registry, _) = NewRegistry();
        registry.Apply(Snapshot(sequence: 7));

        Assert.True(registry.Apply(Snapshot(sequence: 7)).Accepted);

        var conflicting = registry.Apply(Snapshot(sequence: 7, members:
        [
            new TenantMember(new TenantMemberIdentity(Issuer, "subject-1"), ["TenantOwner"])
        ]));

        Assert.Equal(ReasonCodes.MembershipSnapshotInvalid, conflicting.ReasonCode);
        Assert.Equal(["Reader"], registry.Resolve(Issuer, "subject-1").Roles);
    }

    [Fact]
    public void Membership_stops_at_valid_until_and_a_stale_snapshot_cannot_be_applied()
    {
        var (registry, clock) = NewRegistry();
        registry.Apply(Snapshot());

        clock.Set(Issued.AddMinutes(5).AddTicks(-1));
        Assert.True(registry.Resolve(Issuer, "subject-1").IsMember);

        clock.Set(Issued.AddMinutes(5));
        var expired = registry.Resolve(Issuer, "subject-1");
        Assert.False(expired.IsMember);
        Assert.Equal(ReasonCodes.MembershipSnapshotExpired, expired.ReasonCode);

        Assert.Equal(ReasonCodes.MembershipSnapshotExpired, registry.Apply(Snapshot(sequence: 8)).ReasonCode);
    }

    [Fact]
    public void Clock_regression_fails_closed()
    {
        var (registry, clock) = NewRegistry();
        registry.Apply(Snapshot());

        clock.Set(Issued.AddSeconds(-30));

        Assert.Equal(ReasonCodes.EntitlementClockRegression, registry.Resolve(Issuer, "subject-1").ReasonCode);
    }

    [Fact]
    public void Malformed_snapshots_are_rejected()
    {
        var (registry, _) = NewRegistry();
        var identity = new TenantMemberIdentity(Issuer, "subject-1");

        TenantMembershipSnapshot[] malformed =
        [
            Snapshot(sequence: 0),
            Snapshot(members: [new TenantMember(identity, [])]),
            Snapshot(members: [new TenantMember(identity, ["Reader", "Reader"])]),
            Snapshot(members: [new TenantMember(identity, [" "])]),
            Snapshot(members: [new TenantMember(new TenantMemberIdentity("", "s"), ["Reader"])]),
            Snapshot(members: [new TenantMember(identity, ["Reader"]), new TenantMember(identity, ["Contributor"])]),
            Snapshot() with { ValidUntil = Issued }
        ];

        Assert.All(malformed, snapshot =>
        {
            var result = registry.Apply(snapshot);
            Assert.Equal(ReasonCodes.MembershipSnapshotInvalid, result.ReasonCode);
        });
    }
}

internal sealed class ManualClock(DateTimeOffset now) : TimeProvider
{
    private DateTimeOffset _now = now;

    public void Set(DateTimeOffset value) => _now = value;

    public override DateTimeOffset GetUtcNow() => _now;
}
