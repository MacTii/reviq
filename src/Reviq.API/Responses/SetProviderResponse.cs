namespace Reviq.API.Responses;

public sealed record SetProviderResponse(bool Success, string Provider, string Model);

public sealed record SetModelResponse(bool Success, string Model);
