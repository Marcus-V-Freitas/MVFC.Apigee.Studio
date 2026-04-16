namespace ApigeeLocalDev.Domain.Entities;

public sealed record PolicyTemplate(
    string Name,
    string Description,
    string Category,
    string XmlContent,
    IReadOnlyList<string> Parameters);
