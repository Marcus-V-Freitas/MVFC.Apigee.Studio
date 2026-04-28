namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents an Apigee API Product in the emulator.
/// </summary>
public sealed record ApiProduct(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("description")] string Description = "",
    [property: JsonPropertyName("approvalType")] string ApprovalType = "auto",
    [property: JsonPropertyName("environments")] IReadOnlyList<string> Environments = null!,
    [property: JsonPropertyName("proxies")] IReadOnlyList<string> Proxies = null!,
    [property: JsonPropertyName("quota")] string Quota = "100",
    [property: JsonPropertyName("quotaInterval")] string QuotaInterval = "1",
    [property: JsonPropertyName("quotaTimeUnit")] string QuotaTimeUnit = "minute")
{
    public ApiProduct() : this("", "", "", "auto", [], [], "100", "1", "minute") { }
}
