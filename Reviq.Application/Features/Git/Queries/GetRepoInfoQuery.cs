using Mediator;
using Reviq.Domain.Entities;
using Reviq.Domain.Enums;

namespace Reviq.Application.Features.Git.Queries;

public sealed record GetRepoInfoQuery(
    string RepoPath,
    DiffScope Scope = DiffScope.LastCommit,
    string? CommitHash = null) : IRequest<RepoInfo>;
