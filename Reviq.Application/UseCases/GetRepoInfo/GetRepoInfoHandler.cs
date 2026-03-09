using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;

namespace Reviq.Application.UseCases.GetRepoInfo;

public class GetRepoInfoHandler(IGitProvider gitProvider)
{
    public Task<RepoInfo> HandleAsync(GetRepoInfoQuery query) =>
        gitProvider.GetRepoInfoAsync(query.RepoPath);
}
