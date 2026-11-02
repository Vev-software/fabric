using System.Text.Json.Serialization;

namespace Vev.Fabric.Contracts.Customers;

/// <summary>
/// Opaque, stable customer identifier shared by every product. It is compared exactly and is
/// never an e-mail address, a name or any other personal data.
/// </summary>
/// <param name="CustomerId">Opaque customer identifier.</param>
public readonly record struct CustomerRef(string CustomerId)
{
    [JsonIgnore]
    public bool IsPresent => !string.IsNullOrWhiteSpace(CustomerId);

    public override string ToString() => CustomerId;
}

/// <summary>How an enrolment came about.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProductEnrolmentSource>))]
public enum ProductEnrolmentSource
{
    Paid,
    Gift,
    Trial,
    Bundle
}

/// <summary>Whether an enrolment currently confers access to the product.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ProductEnrolmentStatus>))]
public enum ProductEnrolmentStatus
{
    Active,
    Suspended,
    Ended
}

/// <summary>
/// One customer's enrolment in one product. Carries identifiers and a period only: no price,
/// no payment concept and no product domain.
/// </summary>
/// <param name="ProductId">Stable lower-case product identifier.</param>
/// <param name="OfferId">Opaque issuer-chosen offer identifier; no price, plan or tier meaning is implied.</param>
/// <param name="Source">How the enrolment came about.</param>
/// <param name="Status">Whether the enrolment currently confers access.</param>
/// <param name="From">Start of the enrolment period.</param>
/// <param name="Until">End of the enrolment period; <c>null</c> means open-ended.</param>
public sealed record ProductEnrolment(
    string ProductId,
    string OfferId,
    ProductEnrolmentSource Source,
    ProductEnrolmentStatus Status,
    DateTimeOffset From,
    DateTimeOffset? Until = null);

/// <summary>
/// Portable, versioned statement of the products one customer is enrolled in (fabric#69). Data
/// only: it names the customer by <see cref="CustomerRef"/> and contains no personal data. A
/// control plane emits it; a product reads it and decides separately whether to trust it.
/// </summary>
/// <param name="SchemaVersion">Contract version; consumers reject versions they do not know.</param>
/// <param name="Customer">The customer the enrolments belong to.</param>
/// <param name="Sequence">Monotonic revision for the customer, starting at 1.</param>
/// <param name="IssuedAt">When the snapshot was produced.</param>
/// <param name="Enrolments">The complete set of enrolments, at most one per product.</param>
public sealed record CustomerEnrolmentSnapshot(
    int SchemaVersion,
    CustomerRef Customer,
    long Sequence,
    DateTimeOffset IssuedAt,
    IReadOnlyCollection<ProductEnrolment> Enrolments)
{
    /// <summary>The only schema version this release understands.</summary>
    public const int CurrentSchemaVersion = 1;
}
