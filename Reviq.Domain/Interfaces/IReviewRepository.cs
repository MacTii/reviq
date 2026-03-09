using Reviq.Domain.Entities;

namespace Reviq.Domain.Interfaces;

public interface IReviewRepository
{
    Task SaveAsync(ReviewResult result);
    Task<ReviewResult?> GetByIdAsync(string reviewId);
    Task<List<ReviewResult>> GetAllAsync(int limit = 20);
}