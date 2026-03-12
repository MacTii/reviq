namespace Reviq.Domain.Entities;

public record ProviderInfo(
    string Name,
    string Label,
    string Type,
    string BaseUrl = "",
    bool HasConfig = true
);