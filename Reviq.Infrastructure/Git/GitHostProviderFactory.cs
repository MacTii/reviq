using Reviq.Application.Interfaces;
using Reviq.Domain.Interfaces;

namespace Reviq.Infrastructure.Git;

public class GitHostProviderFactory(
    GitHubProvider gitHub,
    GitLabProvider gitLab) : IGitHostProviderFactory
{
    public IGitHostProvider Create(string platform) => platform.ToLower() switch
    {
        "github" => gitHub,
        "gitlab" => gitLab,
        _ => throw new NotSupportedException($"Platform '{platform}' is not supported. Use 'github' or 'gitlab'.")
    };
}