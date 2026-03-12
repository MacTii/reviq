namespace Reviq.Domain.Enums;

/// <summary>Zakres plików do analizy w repozytorium Git.</summary>
public enum DiffScope
{
    /// <summary>Tylko pliki zmienione w ostatnim commicie (HEAD~1..HEAD).</summary>
    LastCommit = 0,
    /// <summary>Pliki zmienione od ostatniego pusha (origin/branch..HEAD).</summary>
    SinceLastPush = 1,
    /// <summary>Niezacommitowane zmiany (staged + unstaged).</summary>
    Uncommitted = 2,
    /// <summary>Wszystkie pliki w repo.</summary>
    AllFiles = 3
}