using Reviq.Domain.Entities;

namespace Reviq.Application.DTOs;

public sealed record ReviewListItemDto(
    string ReviewId,
    DateTime CreatedAt,
    string Label,
    string Source,
    int OverallScore,
    int Critical,
    int Warnings,
    int Info,
    int FileCount)
{
    public static ReviewListItemDto FromDomain(ReviewResult r) => new(
        r.ReviewId, r.CreatedAt, r.Label, r.Source,
        r.Summary.OverallScore, r.Summary.Critical, r.Summary.Warnings, r.Summary.Info,
        r.Files.Count);
}
