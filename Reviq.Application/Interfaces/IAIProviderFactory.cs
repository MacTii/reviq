using Reviq.Domain.Entities;
using Reviq.Domain.Enums;

namespace Reviq.Application.Interfaces;

public interface IAIProviderFactory
{
    IAIProvider GetProvider(ProviderName name);
    IAIProvider GetCurrent();
    void SetCurrent(ProviderName name);
    IEnumerable<ProviderName> GetAvailableProviders();
    IEnumerable<ProviderInfo> GetConfiguredProviders();
}