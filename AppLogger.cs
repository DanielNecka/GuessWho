namespace GuessWho;

/// <summary>
/// Centralny logger aplikacji - zbiera logi i powiadamia subskrybentów.
/// </summary>
public static class AppLogger
{
    private static readonly List<string> _logs = [];
    private static readonly object _lock = new();

    /// <summary>
    /// Zdarzenie wywoływane po dodaniu nowego wpisu logu.
    /// </summary>
    public static event Action<string>? LogAdded;

    /// <summary>
    /// Dodaje wpis logu z automatycznym timestampem.
    /// </summary>
    /// <param name="message">Treść wpisu logu.</param>
    public static void Log(string message)
    {
        string entry = $"[{DateTime.Now:HH:mm:ss.fff}] {message}";
        lock (_lock)
        {
            _logs.Add(entry);
        }
        LogAdded?.Invoke(entry);
    }

    /// <summary>
    /// Pobiera wszystkie dotychczasowe wpisy logu.
    /// </summary>
    /// <returns>Kopia listy wszystkich wpisów.</returns>
    public static IReadOnlyList<string> GetAll()
    {
        lock (_lock)
        {
            return [.. _logs];
        }
    }
}
