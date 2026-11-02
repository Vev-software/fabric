using System.Text.Json;
using System.Text.Json.Nodes;
using Vev.Fabric.Contracts.Customers;

namespace Vev.Fabric.Contracts.Tests;

public sealed class CustomerEnrolmentContractsTests
{
    private const string Schema = "customer-enrolment-snapshot.schema.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly DateTimeOffset Start = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);

    private static JsonNode? ReadFixture(string directory, string file) =>
        JsonNode.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, directory, file)));

    private static bool IsValid(JsonNode? instance) =>
        JsonSchemaTestHelpers.Evaluate(TestSchemas.Load(Schema), instance).IsValid;

    [Fact]
    public void Synthetic_customer_with_two_products_validates_and_round_trips()
    {
        var snapshot = new CustomerEnrolmentSnapshot(
            CustomerEnrolmentSnapshot.CurrentSchemaVersion,
            new CustomerRef("cust-7f3a9c"),
            3,
            Start.AddHours(9),
            [
                new ProductEnrolment("weekli", "offer-family", ProductEnrolmentSource.Paid, ProductEnrolmentStatus.Active, Start),
                new ProductEnrolment("curio", "offer-intro", ProductEnrolmentSource.Trial, ProductEnrolmentStatus.Active, Start, Start.AddDays(14))
            ]);

        var json = JsonSerializer.Serialize(snapshot, SerializerOptions);
        Assert.True(IsValid(JsonNode.Parse(json)));

        var back = JsonSerializer.Deserialize<CustomerEnrolmentSnapshot>(json, SerializerOptions)!;
        Assert.Equal(snapshot.Customer, back.Customer);
        Assert.Equal(snapshot.Sequence, back.Sequence);
        Assert.Equal(snapshot.Enrolments, back.Enrolments);
    }

    [Fact]
    public void Sample_validates_and_deserializes_with_two_products()
    {
        var instance = ReadFixture("samples", "customer-enrolment-snapshot.sample.json");

        Assert.True(IsValid(instance));
        var snapshot = JsonSerializer.Deserialize<CustomerEnrolmentSnapshot>(instance!.ToJsonString(), SerializerOptions)!;
        Assert.Equal(["weekli", "curio"], snapshot.Enrolments.Select(e => e.ProductId));
        Assert.Equal(ProductEnrolmentSource.Trial, snapshot.Enrolments.Last().Source);
    }

    [Theory]
    [InlineData("customer-enrolment-snapshot.unknown-version.json")]
    [InlineData("customer-enrolment-snapshot.email-field.json")]
    [InlineData("customer-enrolment-snapshot.missing-customer-id.json")]
    [InlineData("customer-enrolment-snapshot.unknown-source.json")]
    [InlineData("customer-enrolment-snapshot.bad-product-id.json")]
    [InlineData("customer-enrolment-snapshot.extra-field.json")]
    public void Invalid_fixtures_are_rejected_by_the_schema(string file) =>
        Assert.False(IsValid(ReadFixture("invalid", file)));

    [Fact]
    public void Contract_exposes_no_personal_data_or_commercial_members()
    {
        var names = new[] { typeof(CustomerRef), typeof(ProductEnrolment), typeof(CustomerEnrolmentSnapshot) }
            .SelectMany(t => t.GetProperties().Select(p => p.Name))
            .ToArray();

        Assert.DoesNotContain(names, n => n.Contains("Email", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Price", StringComparison.OrdinalIgnoreCase)
            || n.Contains("Payment", StringComparison.OrdinalIgnoreCase));
    }
}
