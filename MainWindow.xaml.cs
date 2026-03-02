using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuessWho.Models;

namespace GuessWho;

public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GameManager _gameManager = new();
    private NetworkManager? _networkManager;
    private AppConfig? _config;
    private CancellationTokenSource? _connectCts;

    private int _selectedCharacterIndex = -1;
    private bool _localReady;
    private string? _hostFaceId;
    private string? _clientFaceId;

    private string _myFaceId = string.Empty;
    private bool _gameStarted;
    private bool _guessMode;
    private readonly bool[] _crossedOut = new bool[15];

    private readonly Image[] _selectFaces;
    private readonly Image[] _gameFaces;

    public MainWindow()
    {
        InitializeComponent();

        _selectFaces =
        [
            sel0, sel1, sel2, sel3, sel4, sel5, sel6, sel7,
            sel8, sel9, sel10, sel11, sel12, sel13, sel14
        ];

        _gameFaces =
        [
            gf0, gf1, gf2, gf3, gf4, gf5, gf6, gf7,
            gf8, gf9, gf10, gf11, gf12, gf13, gf14
        ];
    }

    private void ShowScreen(string screen)
    {
        startScreen.Visibility = Visibility.Collapsed;
        selectScreen.Visibility = Visibility.Collapsed;
        gameScreen.Visibility = Visibility.Collapsed;
        endScreen.Visibility = Visibility.Collapsed;

        switch (screen)
        {
            case "start": startScreen.Visibility = Visibility.Visible; break;
            case "select": selectScreen.Visibility = Visibility.Visible; break;
            case "game": gameScreen.Visibility = Visibility.Visible; break;
            case "end": endScreen.Visibility = Visibility.Visible; break;
        }
    }

    private async void HostButton_Click(object sender, MouseButtonEventArgs e)
    {
        await StartConnectionAsync(AppRole.Host);
    }

    private async void ClientButton_Click(object sender, MouseButtonEventArgs e)
    {
        await StartConnectionAsync(AppRole.Client);
    }

    private async Task StartConnectionAsync(AppRole role)
    {
        _connectCts?.Cancel();
        _networkManager?.Dispose();

        (string hostIp, int port) = AppConfig.LoadNetworkSettings();
        _config = new AppConfig { Role = role, HostIp = hostIp, Port = port };
        _networkManager = new NetworkManager(_config);
        _networkManager.MessageReceived += OnMessageReceived;
        _connectCts = new CancellationTokenSource();

        try
        {
            await _networkManager.StartAsync(_connectCts.Token);
            ResetSelectScreen();
            ShowScreen("select");
        }
        catch (OperationCanceledException) { }
        catch (Exception)
        {
            ShowScreen("start");
        }
    }

    private void ResetSelectScreen()
    {
        _selectedCharacterIndex = -1;
        _localReady = false;
        _hostFaceId = null;
        _clientFaceId = null;
        foreach (Image face in _selectFaces)
        {
            face.Opacity = 1.0;
            face.RenderTransform = Transform.Identity;
        }
    }

    private void SelectFace_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Image img || img.Tag is not string tag || !int.TryParse(tag, out int index))
            return;

        if (_selectedCharacterIndex >= 0 && _selectedCharacterIndex < _selectFaces.Length)
        {
            _selectFaces[_selectedCharacterIndex].RenderTransform = Transform.Identity;
            _selectFaces[_selectedCharacterIndex].Opacity = 1.0;
        }

        if (_selectedCharacterIndex == index)
        {
            _selectedCharacterIndex = -1;
            return;
        }

        _selectedCharacterIndex = index;
        var scale = new ScaleTransform(1.08, 1.08, img.Width / 2, img.Height / 2);
        img.RenderTransform = scale;
        img.Opacity = 1.0;
    }

    private async void PlayButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (_selectedCharacterIndex < 0 || _networkManager is null || _config is null || _localReady)
            return;

        _localReady = true;
        string selectedFaceId = _gameManager.Faces[_selectedCharacterIndex].Id;

        if (_config.Role == AppRole.Host)
            _hostFaceId = selectedFaceId;
        else
            _clientFaceId = selectedFaceId;

        await _networkManager.SendAsync(
            MessageTypes.CharacterReady,
            new CharacterReadyPayload { FaceId = selectedFaceId });

        if (_config.Role == AppRole.Host)
            await TryStartGameAsHostAsync();
    }

    private async Task TryStartGameAsHostAsync()
    {
        if (_networkManager is null || !_localReady
            || string.IsNullOrWhiteSpace(_hostFaceId)
            || string.IsNullOrWhiteSpace(_clientFaceId))
            return;

        await _networkManager.SendAsync(
            MessageTypes.GameStart,
            new GameStartPayload
            {
                HostFaceId = _hostFaceId,
                ClientFaceId = _clientFaceId
            });

        StartGame(_hostFaceId);
    }

    private void StartGame(string myFaceId)
    {
        _myFaceId = myFaceId;
        _gameStarted = true;
        _guessMode = false;

        for (int i = 0; i < _crossedOut.Length; i++)
        {
            _crossedOut[i] = false;
            _gameFaces[i].Opacity = 1.0;
            _gameFaces[i].RenderTransform = Transform.Identity;
        }

        ShowScreen("game");
    }

    private void GameFace_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_gameStarted || sender is not Image img
            || img.Tag is not string tag || !int.TryParse(tag, out int index))
            return;

        if (_guessMode)
        {
            _guessMode = false;
            _ = SubmitFinalGuessAsync(index);
            return;
        }

        _crossedOut[index] = !_crossedOut[index];
        _gameFaces[index].Opacity = _crossedOut[index] ? 0.5 : 1.0;
    }

    private void GuessButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (_gameStarted)
            _guessMode = true;
    }

    private async Task SubmitFinalGuessAsync(int faceIndex)
    {
        if (_networkManager is null || faceIndex < 0 || faceIndex >= _gameManager.Faces.Count)
            return;

        await _networkManager.SendAsync(
            MessageTypes.FinalGuess,
            new FinalGuessPayload { GuessedFaceId = _gameManager.Faces[faceIndex].Id });
    }

    private void OnMessageReceived(GameMessage message)
    {
        _ = Dispatcher.InvokeAsync(async () => await HandleMessageAsync(message));
    }

    private async Task HandleMessageAsync(GameMessage message)
    {
        switch (message.Type)
        {
            case MessageTypes.CharacterReady:
            {
                CharacterReadyPayload? payload = message.Payload.Deserialize<CharacterReadyPayload>(_jsonOptions);
                if (payload is null || _config?.Role != AppRole.Host)
                    return;

                _clientFaceId = payload.FaceId;
                await TryStartGameAsHostAsync();
                break;
            }
            case MessageTypes.GameStart:
            {
                GameStartPayload? payload = message.Payload.Deserialize<GameStartPayload>(_jsonOptions);
                if (payload is null || _config is null)
                    return;

                string myId = _config.Role == AppRole.Host
                    ? payload.HostFaceId
                    : payload.ClientFaceId;

                StartGame(myId);
                break;
            }
            case MessageTypes.FinalGuess:
            {
                FinalGuessPayload? payload = message.Payload.Deserialize<FinalGuessPayload>(_jsonOptions);
                if (payload is null || _networkManager is null)
                    return;

                bool correct = payload.GuessedFaceId.Equals(_myFaceId, StringComparison.OrdinalIgnoreCase);

                await _networkManager.SendAsync(
                    MessageTypes.GuessResult,
                    new GuessResultPayload
                    {
                        Correct = correct,
                        GuessedFaceId = payload.GuessedFaceId
                    });

                if (correct)
                    ShowEndScreen(false);
                break;
            }
            case MessageTypes.GuessResult:
            {
                GuessResultPayload? payload = message.Payload.Deserialize<GuessResultPayload>(_jsonOptions);
                if (payload is null)
                    return;

                if (payload.Correct)
                    ShowEndScreen(true);
                break;
            }
            case MessageTypes.Rematch:
            {
                ResetSelectScreen();
                ShowScreen("select");
                break;
            }
        }
    }

    private void ShowEndScreen(bool won)
    {
        _gameStarted = false;
        endBanner.Source = new BitmapImage(
            new Uri(won ? "/assets/baners/win.png" : "/assets/baners/lose.png", UriKind.Relative));
        ShowScreen("end");
    }

    private async void PlayAgainButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (_networkManager is null)
            return;

        await _networkManager.SendAsync(MessageTypes.Rematch, new RematchPayload());
        ResetSelectScreen();
        ShowScreen("select");
    }

    protected override void OnClosed(EventArgs e)
    {
        _connectCts?.Cancel();
        _networkManager?.Dispose();
        base.OnClosed(e);
    }
}