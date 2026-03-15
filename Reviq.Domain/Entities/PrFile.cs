namespace Reviq.Domain.Entities;

public sealed record PrFile(
    string FileName,
    string Patch,
    string RawUrl,
    string Status);