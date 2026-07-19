using Reviq.Domain.Enums;

namespace Reviq.Domain.ValueObjects;

public sealed record ProviderInfo(
    ProviderName Name,
    ProviderType Type,
    string BaseUrl = "",
    bool HasConfig = true);
