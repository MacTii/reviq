using Mediator;
using Microsoft.Extensions.Logging;
using Reviq.Application.Features.Webhook;
using Reviq.Application.Interfaces;

namespace Reviq.Application.Features.Webhook.Commands;

public sealed class HandleWebhookCommandHandler(
    IGitHostProviderFactory gitHostFactory,
    IPrFileReviewer fileReviewer,
    ILogger<HandleWebhookCommandHandler> logger)
    : IRequestHandler<HandleWebhookCommand>
{
    private const int MaxFilesPerReview = 10;

    public async ValueTask<Unit> Handle(HandleWebhookCommand command, CancellationToken cancellationToken)
    {
        var payload = command.Payload;

        if (payload.Action is not ("opened" or "synchronize" or "update"))
            return Unit.Value;

        logger.LogInformation("Webhook {Platform} PR#{PrNumber} repo:{Repo}",
            payload.Platform, payload.PrNumber, payload.RepoFullName);

        var gitHost = gitHostFactory.Create(payload.Platform);
        var files = await gitHost.GetPrFilesAsync(payload.RepoFullName, payload.PrNumber, payload.Token);
        var supportedFiles = files
            .Where(f => f.Status != "removed" && PrFileLanguageDetector.IsSupported(f.FileName))
            .Take(MaxFilesPerReview)
            .ToList();

        if (supportedFiles.Count == 0)
        {
            await gitHost.PostReviewCommentAsync(payload.RepoFullName, payload.PrNumber,
                "**Reviq** — no supported files to analyze in this PR.", payload.Token);
            return Unit.Value;
        }

        var results = await fileReviewer.ReviewAsync(supportedFiles, payload.Token, cancellationToken);
        var comment = WebhookCommentBuilder.Build(results);

        await gitHost.PostReviewCommentAsync(payload.RepoFullName, payload.PrNumber, comment, payload.Token);

        var overallScore = results.Count > 0 ? results.Average(r => r.Score) : 100;
        await gitHost.SetCommitStatusAsync(payload.RepoFullName, payload.CommitSha,
            overallScore >= 70,
            $"Reviq: score {(int)overallScore}/100 · {results.Sum(r => r.Issues.Count)} issues",
            payload.Token);

        return Unit.Value;
    }
}
