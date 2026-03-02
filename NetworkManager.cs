using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using GuessWho.Models;

namespace GuessWho;

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

    public NetworkManager(AppConfig config)
    {
        _config = config;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_config.Role == AppRole.Host)
        {
            StartBroadcasting(_config.Port);

            _listener = new TcpListener(IPAddress.Any, _config.Port);
            _listener.Start();
            ConnectionStatusChanged?.Invoke($"Waiting for client on port {_config.Port}...");
            _client = await _listener.AcceptTcpClientAsync(cancellationToken);
            ConnectionStatusChanged?.Invoke("Client connected.");

            StopBroadcasting();
        }
        else
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
                _client.Dispose();
                _client = new TcpClient();
                ConnectionStatusChanged?.Invoke(
                    $"Connecting to {hostIp}:{port} failed. Retrying 127.0.0.1:{port}...");
                await ConnectWithTimeoutAsync(_client, IPAddress.Loopback, port, cancellationToken);
            }

            ConnectionStatusChanged?.Invoke("Connected to host.");
        }

        NetworkStream stream = _client.GetStream();
        _reader = new StreamReader(stream);
        _writer = new StreamWriter(stream) { AutoFlush = true };
        _receiveCts = new CancellationTokenSource();
        _ = Task.Run(() => ReceiveLoopAsync(_receiveCts.Token), _receiveCts.Token);
    }

    private void StartBroadcasting(int gamePort)
    {
        _broadcastCts = new CancellationTokenSource();
        _ = BroadcastLoopAsync(gamePort, _broadcastCts.Token);
    }

    private void StopBroadcasting()
    {
        _broadcastCts?.Cancel();
    }

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

    private static async Task<(string HostIp, int Port)?> DiscoverHostAsync(
        TimeSpan timeout, CancellationToken ct)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(timeout);

        try
        {
            using var udp = new UdpClient();
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            udp.Client.Bind(new IPEndPoint(IPAddress.Any, DiscoveryPort));

            var result = await udp.ReceiveAsync(timeoutCts.Token);
            string message = Encoding.UTF8.GetString(result.Buffer);
            string prefix = $"{BeaconPrefix}:";

            if (message.StartsWith(prefix)
                && int.TryParse(message[prefix.Length..], out int port))
            {
                return (result.RemoteEndPoint.Address.ToString(), port);
            }
        }
        catch (OperationCanceledException) { }
        catch { }

        return null;
    }

    private static bool IsLoopbackHost(string hostIp)
    {
        if (hostIp.Equals("localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(hostIp, out IPAddress? parsed) && IPAddress.IsLoopback(parsed);
    }

    private static bool IsConnectFailure(Exception ex, CancellationToken cancellationToken)
    {
        return ex is SocketException
            || ex is TimeoutException
            || ex is OperationCanceledException && !cancellationToken.IsCancellationRequested;
    }

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

    public async Task SendAsync<T>(string type, T payload)
    {
        if (_writer is null)
        {
            throw new InvalidOperationException("Cannot send before network is started.");
        }

        string json = JsonSerializer.Serialize(
            new
            {
                type,
                payload
            },
            _serializerOptions);

        await _writer.WriteLineAsync(json);
    }

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
    }

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
