using Reviq.Domain.Enums;

namespace Reviq.Domain.Entities;

public record ProviderInfo(
    ProviderName Name,
    ProviderType Type,
    string BaseUrl = "",
    bool HasConfig = true
)
{
    // Pomocnicze właściwości dla serializacji do JSON (frontend oczekuje stringów)
    public string NameString => Name.ToString();
    public string TypeString => Type.ToString().ToLower();
}