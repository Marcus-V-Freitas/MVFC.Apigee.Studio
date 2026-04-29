using System.Xml.Schema;

namespace MVFC.Apigee.Studio.Infrastructure.Validators;

/// <summary>
/// Implementation of <see cref="IPolicyValidator"/> using <see cref="XmlReader"/> and XSD schemas.
/// </summary>
public sealed class ApigeeXmlPolicyValidator : IPolicyValidator
{
    private static readonly Lazy<IReadOnlyDictionary<string, XmlSchemaSet>> _schemas =
        new(BuildSchemas);

    /// <summary>
    /// Validates the XML content of a policy against its corresponding XSD schema.
    /// </summary>
    /// <param name="xmlContent">The XML string to validate.</param>
    /// <param name="policyType">The type of the Apigee policy (e.g., 'AssignMessage').</param>
    /// <returns>A list of validation errors, if any.</returns>
    public IReadOnlyList<PolicyValidationError> Validate(string xmlContent, string policyType)
    {
        var errors = new List<PolicyValidationError>();

        if (!_schemas.Value.TryGetValue(policyType, out var schemaSet))
        {
            return errors; // Schema not found for this policy type - silent skip
        }

        var settings = new XmlReaderSettings
        {
            ValidationType = ValidationType.Schema,
            Schemas = schemaSet,
        };

        settings.ValidationEventHandler += (_, e) =>
        {
            errors.Add(new PolicyValidationError(
                Line: e.Exception.LineNumber,
                Column: e.Exception.LinePosition,
                Message: e.Message,
                Severity: e.Severity == XmlSeverityType.Error ? "error" : "warning"
            ));
        };

        try
        {
            using var reader = XmlReader.Create(new StringReader(xmlContent), settings);
            while (reader.Read()) { /*nothing*/ }
        }
        catch (XmlException ex)
        {
            errors.Add(new PolicyValidationError(
                Line: ex.LineNumber,
                Column: ex.LinePosition,
                Message: ex.Message,
                Severity: "error"
            ));
        }
        catch (Exception ex)
        {
            errors.Add(new PolicyValidationError(1, 1, ex.Message, "error"));
        }

        return errors;
    }

    /// <summary>
    /// Builds the schema map by loading XSD resources defined in the SchemaManifest.
    /// </summary>
    private static Dictionary<string, XmlSchemaSet> BuildSchemas()
    {
        var map = new Dictionary<string, XmlSchemaSet>(StringComparer.OrdinalIgnoreCase);
        var assembly = typeof(ApigeeXmlPolicyValidator).Assembly;

        foreach (var (policyType, resourceName) in SchemaManifest.Entries)
        {
            try
            {
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    continue;
                }

                var set = new XmlSchemaSet();
                set.Add(targetNamespace: null, schemaDocument: XmlReader.Create(stream));
                set.Compile();
                map[policyType] = set;
            }
            catch
            {
                // Log or ignore schema loading failures
            }
        }

        return map;
    }
}
