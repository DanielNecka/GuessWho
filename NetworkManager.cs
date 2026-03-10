using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GuessWho.Models;

namespace GuessWho;

/// <summary>
/// Zarządza komunikacją sieciową TCP między hostem a klientem w grze GuessWho.
/// Obsługuje wykrywanie hosta przez UDP broadcast, nawiązywanie połączenia oraz wysyłanie/odbieranie wiadomości.
/// </summary>
public sealed class NetworkManager : IDisposable
{
    private static readonly TimeSpan _connectTimeout = TimeSpan.FromSeconds(15);
    private const int DiscoveryPort = 5001;
    private const string BeaconPrefix = "GUESSWHO";

    private readonly AppConfig _config;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web);
    private TcpListener? _listener;
    private TcpClient? _client;
    private StreamReader? _reader;
    private StreamWriter? _writer;
    private CancellationTokenSource? _receiveCts;
    private CancellationTokenSource? _broadcastCts;

    public event Action<GameMessage>? MessageReceived;
    public event Action<string>? ConnectionStatusChanged;

    /// <summary>
    /// Inicjalizuje nowy NetworkManager z podaną konfiguracją.
    /// </summary>
    /// <param name="config">Konfiguracja zawierająca rolę (Host/Client), adres IP oraz port.</param>
    public NetworkManager(AppConfig config)
    {
        _config = config;
    }

    /// <summary>
    /// Rozpoczyna połączenie sieciowe w zależności od roli (Host nasluchuje, Client łączy się).
    /// </summary>
    /// <param name="cancellationToken">Token anulowania operacji.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_config.Role == AppRole.Host)
        {
            await StartAsHostAsync(cancellationToken);
        }
        else
        {
            await StartAsClientAsync(cancellationToken);
        }

        InitializeStreams();
        StartReceiveLoop();
    }

    /// <summary>
    /// Uruchamia serwer jako host - nasłuchuje na połączenia od klienta i rozgłasza beacon UDP.
    /// </summary>
    /// <param name="cancellationToken">Token anulowania operacji.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task StartAsHostAsync(CancellationToken cancellationToken)
    {
        StartBroadcasting(_config.Port);

        _listener = new TcpListener(IPAddress.Any, _config.Port);
        _listener.Start();
        ConnectionStatusChanged?.Invoke($"Waiting for client on port {_config.Port}...");
        _client = await _listener.AcceptTcpClientAsync(cancellationToken);
        ConnectionStatusChanged?.Invoke("Client connected.");

        StopBroadcasting();
    }

    /// <summary>
    /// Uruchamia klienta - próbuje wykryć hosta przez UDP, następnie łączy się przez TCP.
    /// </summary>
    /// <param name="cancellationToken">Token anulowania operacji.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task StartAsClientAsync(CancellationToken cancellationToken)
    {
        string hostIp = _config.HostIp;
        int port = _config.Port;

        var discovered = await DiscoverHostAsync(TimeSpan.FromSeconds(15), cancellationToken);
        if (discovered is not null)
        {
            hostIp = discovered.Value.HostIp;
            port = discovered.Value.Port;
        }

        _client = new TcpClient();
        ConnectionStatusChanged?.Invoke($"Connecting to {hostIp}:{port}...");

        try
        {
            await ConnectWithTimeoutAsync(_client, hostIp, port, cancellationToken);
        }
        catch (Exception ex) when (!IsLoopbackHost(hostIp) && IsConnectFailure(ex, cancellationToken))
        {
            await RetryConnectionToLoopbackAsync(port, cancellationToken);
        }

        ConnectionStatusChanged?.Invoke("Connected to host.");
    }

    /// <summary>
    /// Ponawia próbę połączenia do 127.0.0.1 w przypadku niepowodzenia połączenia do zdalnego hosta.
    /// </summary>
    /// <param name="port">Port do połączenia.</param>
    /// <param name="cancellationToken">Token anulowania operacji.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task RetryConnectionToLoopbackAsync(int port, CancellationToken cancellationToken)
    {
        _client?.Dispose();
        _client = new TcpClient();
        ConnectionStatusChanged?.Invoke($"Connecting to {_config.HostIp}:{port} failed. Retrying 127.0.0.1:{port}...");
        await ConnectWithTimeoutAsync(_client, IPAddress.Loopback, port, cancellationToken);
    }

    /// <summary>
    /// Inicjalizuje strumienie czytania i pisania z połączenia TCP.
    /// </summary>
    private void InitializeStreams()
    {
        if (_client is null)
            return;

        NetworkStream stream = _client.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
    }

    /// <summary>
    /// Rozpoczyna pętlę odbierania wiadomości w tle.
    /// </summary>
    private void StartReceiveLoop()
    {
        _receiveCts = new CancellationTokenSource();
        _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);
    }

    /// <summary>
    /// Rozpoczyna rozgłaszanie beacon UDP informującego klientów o dostępności hosta.
    /// </summary>
    /// <param name="gamePort">Port gry TCP, który będzie rozgłaszany.</param>
    private void StartBroadcasting(int gamePort)
    {
        _broadcastCts = new CancellationTokenSource();
        _ = BroadcastLoopAsync(gamePort, _broadcastCts.Token);
    }

    /// <summary>
    /// Zatrzymuje rozgłaszanie beacon UDP.
    /// </summary>
    private void StopBroadcasting()
    {
        _broadcastCts?.Cancel();
    }

    /// <summary>
    /// Pętla rozgłaszająca beacon UDP co sekundę, informująca klientów o dostępności hosta.
    /// </summary>
    /// <param name="gamePort">Port gry TCP do rozgłaszania.</param>
    /// <param name="ct">Token anulowania pętli.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private static async Task BroadcastLoopAsync(int gamePort, CancellationToken ct)
    {
        try
        {
            using var udp = new UdpClient { EnableBroadcast = true };
            byte[] data = Encoding.UTF8.GetBytes($"{BeaconPrefix}:{gamePort}");
            var endpoint = new IPEndPoint(IPAddress.Broadcast, DiscoveryPort);

            while (!ct.IsCancellationRequested)
            {
                await udp.SendAsync(data, data.Length, endpoint);
                await Task.Delay(1000, ct);
            }
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    /// <summary>
    /// Próbuje wykryć hosta przez nasłuchiwanie beacon UDP z timeoutem.
    /// </summary>
    /// <param name="timeout">Maksymalny czas oczekiwania na wykrycie hosta.</param>
    /// <param name="ct">Token anulowania operacji.</param>
    /// <returns>Krotka z IP hosta i portem jeśli wykryto, w przeciwnym razie null.</returns>
    private static async Task<(string HostIp, int Port)?> DiscoverHostAsync(
        TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            using var udp = CreateDiscoveryUdpClient();
            var result = await udp.ReceiveAsync(timeoutCts.Token);
            return ParseBeaconMessage(result);
        }
        catch (OperationCanceledException) { }
        catch { }

        return null;
    }

    /// <summary>
    /// Tworzy klienta UDP do wykrywania hostów nasłuchującego na porcie discovery.
    /// </summary>
    /// <returns>Skonfigurowany UdpClient.</returns>
    private static UdpClient CreateDiscoveryUdpClient()
    {
        var udp = new UdpClient();
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));
        return udp;
    }

    /// <summary>
    /// Parsuje odebraną wiadomość beacon UDP.
    /// </summary>
    /// <param name="result">Wynik odbioru UDP.</param>
    /// <returns>Krotka z IP hosta i portem jeśli wiadomość jest poprawna, w przeciwnym razie null.</returns>
    private static (string HostIp, int Port)? ParseBeaconMessage(UdpReceiveResult result)
    {
        string message = Encoding.UTF8.GetString(result.Buffer);
        string prefix = $"{BeaconPrefix}:";

        if (message.StartsWith(prefix) && int.TryParse(message[prefix.Length..], out int port))
        {
            return (result.RemoteEndPoint.Address.ToString(), port);
        }

        return null;
    }

    /// <summary>
    /// Sprawdza czy podany host IP jest adresem loopback.
    /// </summary>
    /// <param name="hostIp">Adres IP do sprawdzenia.</param>
    /// <returns>True jeśli jest loopback, w przeciwnym razie false.</returns>
    private static bool IsLoopbackHost(string hostIp)
    {
        if (hostIp.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(hostIp, out IPAddress? parsed) && IPAddress.IsLoopback(parsed);
    }

    /// <summary>
    /// Sprawdza czy wyjątek oznacza błąd połączenia (nie anulowanie przez użytkownika).
    /// </summary>
    /// <param name="ex">Wyjątek do sprawdzenia.</param>
    /// <param name="cancellationToken">Token anulowania użytkownika.</param>
    /// <returns>True jeśli to błąd połączenia, false jeśli anulowanie użytkownika.</returns>
    private static bool IsConnectFailure(Exception ex, CancellationToken cancellationToken)
    {
        return ex is SocketException
            || ex is TimeoutException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested;
    }

    /// <summary>
    /// Łączy TcpClient z hostem z timeoutem.
    /// </summary>
    /// <param name="client">Klient TCP do połączenia.</param>
    /// <param name="host">Adres hosta.</param>
    /// <param name="port">Port hosta.</param>
    /// <param name="cancellationToken">Token anulowania operacji.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private static async Task ConnectWithTimeoutAsync(
        TcpClient client,
        string host,
        int port,
        CancellationToken cancellationToken)
    {
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(_connectTimeout);

        try
        {
            await client.ConnectAsync(host, port, connectCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Connection to {host}:{port} timed out.");
        }
    }

    /// <summary>
    /// Łączy TcpClient z adresem IP z timeoutem.
    /// </summary>
    /// <param name="client">Klient TCP do połączenia.</param>
    /// <param name="ipAddress">Adres IP hosta.</param>
    /// <param name="port">Port hosta.</param>
    /// <param name="cancellationToken">Token anulowania operacji.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private static async Task ConnectWithTimeoutAsync(
        TcpClient client,
        IPAddress ipAddress,
        int port,
        CancellationToken cancellationToken)
    {
        using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        connectCts.CancelAfter(_connectTimeout);

        try
        {
            await client.ConnectAsync(ipAddress, port, connectCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Connection to {ipAddress}:{port} timed out.");
        }
    }

    /// <summary>
    /// Wysyła wiadomość do połączonego peera.
    /// </summary>
    /// <typeparam name="T">Typ payloadu wiadomości.</typeparam>
    /// <param name="type">Typ wiadomości (stała z MessageTypes).</param>
    /// <param name="payload">Dane payloadu do wysłania.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    public async Task SendAsync<T>(string type, T payload)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Cannot send before network is started.");
        }

        string json = SerializeMessage(type, payload);
        await _writer.WriteLineAsync(json);
    }

    /// <summary>
    /// Serializuje wiadomość do formatu JSON.
    /// </summary>
    /// <typeparam name="T">Typ payloadu.</typeparam>
    /// <param name="type">Typ wiadomości.</param>
    /// <param name="payload">Payload do serializacji.</param>
    /// <returns>String JSON reprezentujący wiadomość.</returns>
    private string SerializeMessage<T>(string type, T payload)
    {
        return JsonSerializer.Serialize(
            new
            {
                type,
                payload
            },
            _serializerOptions);
    }

    /// <summary>
    /// Pętla odbierająca wiadomości od peera. Działa w tle do momentu anulowania lub rozłączenia.
    /// </summary>
    /// <param name="cancellationToken">Token anulowania pętli.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        if (_reader is null)
        {
            return;
        }

        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await _reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                ConnectionStatusChanged?.Invoke("Disconnected.");
                return;
            }

            ProcessReceivedMessage(line);
        }
    }

    /// <summary>
    /// Przetwarza odebraną linię tekstu jako wiadomość JSON i wywołuje event MessageReceived.
    /// </summary>
    /// <param name="line">Odebrana linia JSON.</param>
    private void ProcessReceivedMessage(string line)
    {
        try
        {
            GameMessage? message = JsonSerializer.Deserialize<GameMessage>(line, _serializerOptions);
            if (message is not null)
            {
                MessageReceived?.Invoke(message);
            }
        }
        catch (JsonException)
        {
            ConnectionStatusChanged?.Invoke("Received invalid message format.");
        }
    }

    /// <summary>
    /// Zwalnia zasoby sieciowe - zatrzymuje broadcasty, pętle odbierania, zamyka strumienie i połączenia.
    /// </summary>
    public void Dispose()
    {
        _broadcastCts?.Cancel();
        _receiveCts?.Cancel();
        _reader?.Dispose();
        _writer?.Dispose();
        _client?.Dispose();
        _listener?.Stop();
    }
}
