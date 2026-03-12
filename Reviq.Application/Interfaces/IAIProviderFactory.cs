using Reviq.Domain.Entities;

namespace Reviq.Application.Interfaces;

public interface IAIProviderFactory
{
    IAIProvider GetProvider(string providerName);
    IAIProvider GetCurrent();
    void SetCurrent(string providerName, string? apiKey = null, string? baseUrl = null);
    IEnumerable<string> GetAvailableProviders();
    IEnumerable<ProviderInfo> GetConfiguredProviders();
}