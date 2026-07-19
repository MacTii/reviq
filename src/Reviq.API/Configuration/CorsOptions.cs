namespace Reviq.API.Configuration;

public sealed class CorsOptions
{
    public const string Section = "Cors";

    public string[] AllowedOrigins { get; init; } = ["http://localhost:5000"];
}
