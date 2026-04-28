namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Generic name-value property used in various Apigee entities.
/// </summary>
public sealed record ApiProperty(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("value")] string Value);
