using Mediator;
using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;

namespace Reviq.Application.Features.Git.Queries;

public sealed class GetRepoInfoQueryHandler(IGitProvider gitProvider)
    : IRequestHandler<GetRepoInfoQuery, RepoInfo>
{
    public ValueTask<RepoInfo> Handle(GetRepoInfoQuery query, CancellationToken cancellationToken)
        => new(gitProvider.GetRepoInfoAsync(query.RepoPath, query.Scope, query.CommitHash));
}
