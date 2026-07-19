using Mediator;
using Reviq.Application.DTOs;
using Reviq.Application.Interfaces;
using Reviq.Domain.Enums;

namespace Reviq.Application.Features.AI.Queries;

public sealed class GetProviderStatusQueryHandler(
    IAIProviderFactory providerFactory,
    ILocalAIService localAIService) : IRequestHandler<GetProviderStatusQuery, ProviderStatusDto?>
{
    private static readonly TimeSpan AvailabilityCheckTimeout = TimeSpan.FromSeconds(3);

    public async ValueTask<ProviderStatusDto?> Handle(GetProviderStatusQuery query, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ProviderName>(query.ProviderName, ignoreCase: true, out var providerName))
            return null;

        // LocalAI — zawsze dostępny (lokalny provider), modele opcjonalne
        if (providerName == ProviderName.LocalAI)
        {
            var installed = await localAIService.GetInstalledModelsAsync();
            var models = installed.Models.Select(m => m.FileName).ToList();
            return new ProviderStatusDto(query.ProviderName, Available: true, models);
        }

        // Jeśli provider wymaga konfiguracji (np. API key) a nie jest skonfigurowany,
        // traktujemy go jako niedostępny zamiast wykonywać zewnętrzne żądanie
        var configured = providerFactory.GetConfiguredProviders().FirstOrDefault(p => p.Name == providerName);
        if (configured != null && !configured.HasConfig)
            return new ProviderStatusDto(query.ProviderName, Available: false, Array.Empty<string>());

        var provider = providerFactory.GetProvider(providerName);

        // Krótki timeout dla sprawdzania dostępności - nie blokuj UI
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(AvailabilityCheckTimeout);

        bool available;
        try { available = await provider.IsAvailableAsync(); }
        catch { available = false; }

        var modelList = available ? await provider.GetAvailableModelsAsync() : new List<string>();
        return new ProviderStatusDto(query.ProviderName, available, modelList);
    }
}
