using Reviq.Domain.Entities;
using Reviq.Domain.Interfaces;
using System.Collections.Concurrent;

namespace Reviq.Infrastructure.Persistence;

/// <summary>
/// In-memory implementation for MVP. Replace with SQLite/PostgreSQL for production.
/// </summary>
public class ReviewRepository : IReviewRepository
{
    private readonly ConcurrentDictionary<string, ReviewResult> _store = new();

    public Task SaveAsync(ReviewResult result)
    {
        _store[result.ReviewId] = result;
        return Task.CompletedTask;
    }

    public Task<ReviewResult?> GetByIdAsync(string reviewId)
    {
        _store.TryGetValue(reviewId, out var result);
        return Task.FromResult(result);
    }

    public Task<List<ReviewResult>> GetAllAsync(int limit = 20)
    {
        var results = _store.Values
            .OrderByDescending(r => r.CreatedAt)
            .Take(limit)
            .ToList();
        return Task.FromResult(results);
    }
}
