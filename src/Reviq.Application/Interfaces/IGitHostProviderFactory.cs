using Reviq.Domain.Interfaces;

namespace Reviq.Application.Interfaces;

public interface IGitHostProviderFactory
{
    IGitHostProvider Create(string platform);
}
