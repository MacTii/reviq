namespace Reviq.Domain.Entities;

public class PrFile
{
    public string FileName { get; set; } = "";
    public string Patch { get; set; } = "";   // diff
    public string RawUrl { get; set; } = "";  // link do pobrania pliku
    public string Status { get; set; } = "";  // added, modified, removed
}