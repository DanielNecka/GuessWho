using System.IO;
using System.Net;

namespace GuessWho;

public enum AppRole
{
    Host,
    Client
}

public sealed class AppConfig
{
    public AppRole Role { get; init; }
    public string HostIp { get; init; } = "127.0.0.1";
    public int Port { get; init; } = 5000;

    public static (string HostIp, int Port) LoadNetworkSettings()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "config.txt");
        if (!File.Exists(configPath))
        {
            return ("127.0.0.1", 5000);
        }

        Dictionary<string, string> values = new(StringComparer.OrdinalIgnoreCase);

        foreach (string rawLine in File.ReadAllLines(configPath))
        {
            string line = rawLine.Trim();
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith('#'))
            {
                continue;
            }

            int separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0 || separatorIndex == line.Length - 1)
            {
                continue;
            }

            string key = line[..separatorIndex].Trim();
            string value = line[(separatorIndex + 1)..].Trim();
            values[key] = value;
        }

        string hostIp = values.TryGetValue("host_ip", out string? configuredIp)
            ? configuredIp
            : "127.0.0.1";

        if (!IPAddress.TryParse(hostIp, out _))
        {
            hostIp = "127.0.0.1";
        }

        int port = 5000;
        if (values.TryGetValue("port", out string? portValue))
        {
            int.TryParse(portValue, out port);
        }

        return (hostIp, port);
    }
}
