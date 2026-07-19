namespace Reviq.API.Responses;

public sealed record ProviderSummaryResponse(string Name, string Label, string Type, bool HasConfig);

public sealed record ProvidersResponse(IReadOnlyList<ProviderSummaryResponse> Providers, string CurrentProvider);
