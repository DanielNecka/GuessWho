using System.IO;
using System.Net;

namespace GuessWho;

/// <summary>
/// Definiuje rolę aplikacji w sesji gry.
/// </summary>
public enum AppRole
{
    Host,
    Client
}

/// <summary>
/// Przechowuje konfigurację aplikacji, w tym rolę, adres IP hosta oraz port.
/// </summary>
public sealed class AppConfig
{
    public AppRole Role { get; init; }
    public string HostIp { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 5000;

    /// <summary>
    /// Wczytuje ustawienia sieciowe z pliku config.txt.
    /// </summary>
    /// <returns>Krotka zawierająca adres IP hosta oraz numer portu. W przypadku braku pliku lub błędów zwraca wartości domyślne (127.0.0.1, 5000).</returns>
    public static (string HostIp, int Port) LoadNetworkSettings()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "config.txt");
        if (!File.Exists(configPath))
        {
            return ("127.0.0.1", 5000);
        }

        Dictionary<string, string> values = ParseConfigFile(configPath);

        string hostIp = ExtractHostIp(values);
        int port = ExtractPort(values);

        return (hostIp, port);
    }

    /// <summary>
    /// Parsuje plik konfiguracyjny do słownika klucz-wartość.
    /// </summary>
    /// <param name="configPath">Ścieżka do pliku konfiguracyjnego.</param>
    /// <returns>Słownik zawierający pary klucz-wartość z pliku.</returns>
    private static Dictionary<string, string> ParseConfigFile(string configPath)
    {
        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        foreach (string rawLine in File.ReadAllLines(configPath))
        {
            string line = rawLine.Trim();
            if (IsCommentOrEmpty(line))
            {
                continue;
            }

            var kvp = ParseConfigLine(line);
            if (kvp.HasValue)
            {
                values[kvp.Value.Key] = kvp.Value.Value;
            }
        }

        return values;
    }

    /// <summary>
    /// Sprawdza czy linia jest komentarzem lub pusta.
    /// </summary>
    /// <param name="line">Linia do sprawdzenia.</param>
    /// <returns>True jeśli linia jest pusta lub rozpoczyna się od #, w przeciwnym razie false.</returns>
    private static bool IsCommentOrEmpty(string line)
    {
        return string.IsNullOrWhiteSpace(line) || line.StartsWith('#');
    }

    /// <summary>
    /// Parsuje pojedynczą linię konfiguracji do pary klucz-wartość.
    /// </summary>
    /// <param name="line">Linia do sparsowania.</param>
    /// <returns>Para klucz-wartość jeśli parsowanie się udało, w przeciwnym razie null.</returns>
    private static (string Key, string Value)? ParseConfigLine(string line)
    {
        int separatorIndex = line.IndexOf('=');
        if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
        {
            return null;
        }

        string key = line[..separatorIndex].Trim();
        string value = line[(separatorIndex + 1)..].Trim();
        return (key, value);
    }

    /// <summary>
    /// Wydobywa i waliduje adres IP hosta ze słownika konfiguracji.
    /// </summary>
    /// <param name="values">Słownik z wartościami konfiguracji.</param>
    /// <returns>Walidowany adres IP hosta lub 127.0.0.1 jeśli wartość jest nieprawidłowa.</returns>
    private static string ExtractHostIp(Dictionary<string, string> values)
    {
        string hostIp = values.TryGetValue("host_ip", out string? configuredIp)
            ? configuredIp
            : "127.0.0.1";

        if (!IPAddress.TryParse(hostIp, out _))
        {
            hostIp = "127.0.0.1";
        }

        return hostIp;
    }

    /// <summary>
    /// Wydobywa i waliduje numer portu ze słownika konfiguracji.
    /// </summary>
    /// <param name="values">Słownik z wartościami konfiguracji.</param>
    /// <returns>Numer portu lub 5000 jeśli wartość jest nieprawidłowa lub nie istnieje.</returns>
    private static int ExtractPort(Dictionary<string, string> values)
    {
        int port = 5000;
        if (values.TryGetValue("port", out string? portValue))
        {
            int.TryParse(portValue, out port);
        }

        return port;
    }
}
