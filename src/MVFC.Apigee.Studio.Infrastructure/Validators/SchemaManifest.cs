namespace MVFC.Apigee.Studio.Infrastructure.Validators;

/// <summary>
/// Manifest mapping Apigee policy types to their embedded XSD resource names.
/// </summary>
internal static class SchemaManifest
{
    public static readonly IReadOnlyList<(string PolicyType, string ResourceName)> Entries =
    [
        ("AssignMessage", "MVFC.Apigee.Studio.Infrastructure.Schemas.AssignMessage.xsd"),
        ("VerifyAPIKey", "MVFC.Apigee.Studio.Infrastructure.Schemas.VerifyAPIKey.xsd"),
        ("ExtractVariables", "MVFC.Apigee.Studio.Infrastructure.Schemas.ExtractVariables.xsd"),
        ("Quota", "MVFC.Apigee.Studio.Infrastructure.Schemas.Quota.xsd"),
        ("SpikeArrest", "MVFC.Apigee.Studio.Infrastructure.Schemas.SpikeArrest.xsd"),
    ];
}
