namespace Reviq.API.Requests;

public sealed record SetProviderRequest(string Provider, string? Model = null);