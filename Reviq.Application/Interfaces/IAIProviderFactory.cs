using Reviq.Domain.Enums;
using Reviq.Domain.ValueObjects;

namespace Reviq.Application.Interfaces;

public interface IAIProviderFactory
{
    IAIProvider GetCurrent();
    IAIProvider GetProvider(ProviderName name);
    void SetCurrent(ProviderName name);
    void SetModel(string model);
    IEnumerable<ProviderName> GetAvailableProviders();
    IEnumerable<ProviderInfo> GetConfiguredProviders();
}