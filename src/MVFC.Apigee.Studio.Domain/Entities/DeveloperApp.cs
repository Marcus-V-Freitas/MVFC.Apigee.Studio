namespace MVFC.Apigee.Studio.Domain.Entities;

/// <summary>
/// Represents an Apigee Developer App in the emulator.
/// </summary>
/// <param name="Name">The unique name of the application.</param>
/// <param name="DisplayName">The user-friendly name of the application.</param>
/// <param name="DeveloperId">The unique identifier of the developer (often email).</param>
/// <param name="DeveloperEmail">The email address of the developer.</param>
/// <param name="ApiProducts">The list of API products associated with the app.</param>
/// <param name="AppId">The unique ID assigned by the emulator/Apigee.</param>
/// <param name="CallbackUrl">The callback URL for the application.</param>
/// <param name="ExpiryType">The expiration policy (e.g., 'never').</param>
/// <param name="Status">The current status of the app (e.g., 'approved').</param>
/// <param name="Credentials">The list of credentials (keys/secrets) for the app.</param>
public sealed record DeveloperApp(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("displayName")] string DisplayName,
    [property: JsonPropertyName("developerId")] string DeveloperId,
    [property: JsonPropertyName("developerEmail")] string DeveloperEmail,
    [property: JsonPropertyName("apiProducts")] IReadOnlyList<string> ApiProducts,
    [property: JsonPropertyName("appId")] string? AppId = null,
    [property: JsonPropertyName("callbackUrl")] string CallbackUrl = "",
    [property: JsonPropertyName("expiryType")] string ExpiryType = "never",
    [property: JsonPropertyName("status")] string Status = "approved",
    [property: JsonPropertyName("credentials")] IReadOnlyList<AppCredential>? Credentials = null)
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeveloperApp"/> record with default values.
    /// </summary>
    public DeveloperApp() : this("", "", "", "", [], null, "", "never", "approved", []) { }
}
