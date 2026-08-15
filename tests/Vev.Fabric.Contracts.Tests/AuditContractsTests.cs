using System.Text.Json;
using Vev.Fabric.Contracts.Audit;

namespace Vev.Fabric.Contracts.Tests;

public sealed class AuditContractsTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public void AuditEvent_RoundTrips()
    {
        var auditEvent = CreateEvent() with
        {
            CausationId = "req-1-parent",
            Metadata = new Dictionary<string, string> { ["changeKind"] = "update" }
        };

        var json = JsonSerializer.Serialize(auditEvent, SerializerOptions);
        var roundTripped = JsonSerializer.Deserialize<AuditEvent>(json, SerializerOptions);

        Assert.NotNull(roundTripped);
        Assert.Equal(auditEvent.EventId, roundTripped!.EventId);
        Assert.Equal(auditEvent.Tenant, roundTripped.Tenant);
        Assert.Equal(auditEvent.Actor.PrincipalId, roundTripped.Actor.PrincipalId);
        Assert.Equal(auditEvent.Action, roundTripped.Action);
        Assert.Equal(auditEvent.Resource.Value, roundTripped.Resource.Value);
        Assert.Equal(AuditCategory.Data, roundTripped.Category);
        Assert.Equal(AuditOutcome.Success, roundTripped.Outcome);
        Assert.Equal("req-1", roundTripped.CorrelationId);
        Assert.Equal("req-1-parent", roundTripped.CausationId);
        Assert.Equal("update", roundTripped.Metadata!["changeKind"]);
    }

    [Fact]
    public void Category_And_Outcome_Serialize_As_Strings()
    {
        var json = JsonSerializer.Serialize(
            CreateEvent() with { Category = AuditCategory.Security, Outcome = AuditOutcome.Denied },
            SerializerOptions);

        Assert.Contains("\"category\":\"Security\"", json);
        Assert.Contains("\"outcome\":\"Denied\"", json);
    }

    [Fact]
    public void Optional_Fields_Are_Omitted_When_Absent()
    {
        var json = JsonSerializer.Serialize(CreateEvent(), SerializerOptions);

        Assert.DoesNotContain("causationId", json);
        Assert.DoesNotContain("metadata", json);
    }

    [Fact]
    public void FromPrincipal_Drops_Claims_But_Keeps_Roles()
    {
        var principal = new PrincipalContext(
            "principal-1",
            "Atlas Architect",
            ["AtlasArchitect"],
            new Dictionary<string, string> { ["email"] = "architect@example.test" });

        var actor = AuditActor.FromPrincipal(principal);

        Assert.Equal("principal-1", actor.PrincipalId);
        Assert.Equal("Atlas Architect", actor.DisplayName);
        Assert.Equal(principal.Roles, actor.Roles);

        // The redaction guarantee: no claim (email, etc.) survives serialization of the actor.
        var json = JsonSerializer.Serialize(actor, SerializerOptions);
        Assert.DoesNotContain("email", json);
        Assert.DoesNotContain("architect@example.test", json);
        Assert.DoesNotContain("claims", json);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("apiKey")]
    [InlineData("access_token")]
    [InlineData("Authorization")]
    [InlineData("session-id")]
    public void Redaction_Rejects_Sensitive_Metadata_Keys(string key)
    {
        var metadata = new Dictionary<string, string> { [key] = "value" };

        Assert.False(AuditRedaction.IsRedactionSafe(metadata, out var offendingKey));
        Assert.Equal(key, offendingKey);
    }

    [Fact]
    public void Redaction_Allows_Ordinary_Metadata_Keys()
    {
        var metadata = new Dictionary<string, string>
        {
            ["changeKind"] = "update",
            ["clientApp"] = "atlas-web"
        };

        Assert.True(AuditRedaction.IsRedactionSafe(metadata, out var offendingKey));
        Assert.Null(offendingKey);
    }

    [Fact]
    public void Log_Is_Append_Only_And_Preserves_Order()
    {
        var log = new InMemoryAuditLog();
        var first = CreateEvent() with { EventId = "e-1" };
        var second = CreateEvent() with { EventId = "e-2" };

        log.Append(first);
        log.Append(second);

        Assert.Equal(["e-1", "e-2"], log.Events.Select(e => e.EventId));
    }

    [Fact]
    public void Log_Rejects_Events_With_Sensitive_Metadata()
    {
        var log = new InMemoryAuditLog();
        var poisoned = CreateEvent() with
        {
            Metadata = new Dictionary<string, string> { ["passwordHash"] = "..." }
        };

        var exception = Assert.Throws<AuditRedactionException>(() => log.Append(poisoned));
        Assert.Equal("passwordHash", exception.OffendingKey);
        Assert.Empty(log.Events);
    }

    [Fact]
    public void Log_Rejects_Events_Missing_Required_Fields()
    {
        var log = new InMemoryAuditLog();

        Assert.Throws<ArgumentException>(() => log.Append(CreateEvent() with { EventId = " " }));
        Assert.Throws<ArgumentException>(() => log.Append(CreateEvent() with { CorrelationId = "" }));
        Assert.Throws<ArgumentException>(() => log.Append(CreateEvent() with { Tenant = new TenantContext("") }));
    }

    [Theory]
    [InlineData(AuditCategory.Admin)]
    [InlineData(AuditCategory.Security)]
    public void Admin_And_Security_Events_Are_Immutable(AuditCategory category)
    {
        Assert.True((CreateEvent() with { Category = category }).IsImmutable);
    }

    [Fact]
    public void Data_Events_Are_Not_Immutable()
    {
        Assert.False((CreateEvent() with { Category = AuditCategory.Data }).IsImmutable);
    }

    private static AuditEvent CreateEvent() =>
        new(
            EventId: "e-0",
            OccurredAt: new DateTimeOffset(2026, 8, 15, 10, 0, 0, TimeSpan.Zero),
            Tenant: new TenantContext("tenant-a"),
            Actor: new AuditActor("principal-1", "Atlas Architect", ["AtlasArchitect"]),
            Source: "atlas",
            Action: "atlas.catalogue.write",
            Resource: new AuditResource("atlas:catalogue/main", "catalogue-entry"),
            Category: AuditCategory.Data,
            Outcome: AuditOutcome.Success,
            CorrelationId: "req-1");
}
