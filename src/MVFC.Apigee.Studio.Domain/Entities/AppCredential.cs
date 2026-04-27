using System.Text.Json.Serialization;

namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents credentials (API Key/Secret) for a Developer App.
/// </summary>
public sealed record AppCredential(
    [property: JsonPropertyName("consumerKey")] string ConsumerKey,
    [property: JsonPropertyName("consumerSecret")] string ConsumerSecret,
    [property: JsonPropertyName("apiProducts")] IReadOnlyList<AppCredentialProduct> ApiProducts,
    [property: JsonPropertyName("status")] string Status = "approved");
