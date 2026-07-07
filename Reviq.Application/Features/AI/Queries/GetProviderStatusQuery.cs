using Mediator;
using Reviq.Application.DTOs;

namespace Reviq.Application.Features.AI.Queries;

public sealed record GetProviderStatusQuery(string ProviderName) : IRequest<ProviderStatusDto?>;
