namespace Reviq.Application.DTOs;

public sealed record ProviderStatusDto(string Name, bool Available, IReadOnlyList<string> Models);
