namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Links a credential to a specific API Product.
/// </summary>
public sealed record AppCredentialProduct(
    [property: JsonPropertyName("apiproduct")] string Apiproduct,
    [property: JsonPropertyName("status")] string Status = "approved");
