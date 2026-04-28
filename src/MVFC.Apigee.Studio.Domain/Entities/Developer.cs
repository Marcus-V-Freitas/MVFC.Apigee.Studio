namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents an Apigee Developer in the emulator.
/// </summary>
public sealed record Developer(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("firstName")] string FirstName,
    [property: JsonPropertyName("lastName")] string LastName,
    [property: JsonPropertyName("userName")] string UserName,
    [property: JsonPropertyName("attributes")] IReadOnlyList<ApiProperty> Attributes = null!)
{
    public Developer() : this("", "", "", "", []) { }
}
