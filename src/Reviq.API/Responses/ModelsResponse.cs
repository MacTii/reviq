namespace Reviq.API.Responses;

public sealed record ModelsResponse(string Provider, IReadOnlyList<string> Models);
