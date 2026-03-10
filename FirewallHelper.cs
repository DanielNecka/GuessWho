using System.Diagnostics;

namespace GuessWho;

/// <summary>
/// Automatycznie dodaje reguły zapory Windows dla portów używanych przez grę.
/// </summary>
public static class FirewallHelper
{
    private const string RulePrefix = "GuessWho";

    /// <summary>
    /// Upewnia się, że reguły zapory istnieją dla podanych portów TCP i UDP.
    /// </summary>
    /// <param name="tcpPort">Port TCP gry.</param>
    /// <param name="udpPort">Port UDP discovery.</param>
    public static void EnsureRulesForPorts(int tcpPort, int udpPort)
    {
        AppLogger.Log($"Sprawdzanie reguł zapory (TCP:{tcpPort}, UDP:{udpPort})...");

        TryAddRule($"{RulePrefix}_TCP_In_{tcpPort}", "TCP", tcpPort, "in");
        TryAddRule($"{RulePrefix}_UDP_In_{udpPort}", "UDP", udpPort, "in");
        TryAddRule($"{RulePrefix}_UDP_Out_{udpPort}", "UDP", udpPort, "out");
    }

    private static void TryAddRule(string name, string protocol, int port, string direction)
    {
        try
        {
            if (RuleExists(name))
            {
                AppLogger.Log($"  Reguła '{name}' już istnieje.");
                return;
            }

            string args = $"advfirewall firewall add rule name=\"{name}\" dir={direction} action=allow protocol={protocol} localport={port} enable=yes";
            int exitCode = RunNetsh(args);

            if (exitCode == 0)
                AppLogger.Log($"  Dodano regułę: {name}");
            else
                AppLogger.Log($"  Nie udało się dodać reguły '{name}' (kod {exitCode}). Uruchom jako administrator lub dodaj ręcznie.");
        }
        catch (Exception ex)
        {
            AppLogger.Log($"  Błąd reguły '{name}': {ex.Message}");
        }
    }

    private static bool RuleExists(string name)
    {
        try
        {
            return RunNetsh($"advfirewall firewall show rule name=\"{name}\"") == 0;
        }
        catch
        {
            return false;
        }
    }

    private static int RunNetsh(string arguments)
    {
        var psi = new ProcessStartInfo("netsh", arguments)
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi)!;
        process.WaitForExit(5000);
        return process.ExitCode;
    }
}
