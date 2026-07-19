namespace Reviq.Application.Interfaces;

public interface IPrFileContentFetcher
{
    Task<string> FetchAsync(string rawUrl, string token, CancellationToken cancellationToken = default);
}
