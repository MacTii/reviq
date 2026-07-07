namespace Reviq.Infrastructure.Configuration;

public sealed class GitOptions
{
    public const string Section = "Git";

    public string UserAgent { get; init; } = "Reviq";
    public string StatusContext { get; init; } = "Reviq / AI Code Review";

    public GitHubOptions GitHub { get; init; } = new();
    public GitLabOptions GitLab { get; init; } = new();
}

public sealed class GitHubOptions
{
    public string BaseUrl { get; init; } = "https://api.github.com";
    public string Token { get; init; } = "";
}

public sealed class GitLabOptions
{
    public string BaseUrl { get; init; } = "https://gitlab.com/api/v4";
    public string Token { get; init; } = "";
}