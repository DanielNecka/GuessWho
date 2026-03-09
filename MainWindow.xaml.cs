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
    private string _enemyFaceId = string.Empty;
    private bool _gameStarted;
    private int _selectedGameIndex = -1;
    private readonly bool[] _crossedOut = new bool[15];
    private readonly bool[] _wronglyGuessed = new bool[15];

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
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Nie udało się połączyć: {ex.Message}\n\n" +
                $"Sprawdź config.txt — host_ip musi wskazywać IP hosta.\n" +
                $"Aktualnie: {_config?.HostIp}:{_config?.Port}",
                "Błąd połączenia", MessageBoxButton.OK, MessageBoxImage.Warning);
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

        StartGame(_hostFaceId, _clientFaceId);
    }

    private void StartGame(string myFaceId, string enemyFaceId)
    {
        _myFaceId = myFaceId;
        _enemyFaceId = enemyFaceId;
        _gameStarted = true;
        _selectedGameIndex = -1;

        for (int i = 0; i < _crossedOut.Length; i++)
        {
            _crossedOut[i] = false;
            _wronglyGuessed[i] = false;
            _gameFaces[i].Opacity = 1.0;
            _gameFaces[i].RenderTransform = Transform.Identity;
            _gameFaces[i].Effect = null;
            _gameFaces[i].Cursor = Cursors.Hand;
        }

        // Set player face image based on selected character
        if (_selectedCharacterIndex >= 0 && _selectedCharacterIndex < _selectFaces.Length)
        {
            var selectedFaceSource = _selectFaces[_selectedCharacterIndex].Source;
            playerFace.Source = selectedFaceSource;
        }

        ShowScreen("game");
    }

    private void GameFace_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_gameStarted || sender is not Image img
            || img.Tag is not string tag || !int.TryParse(tag, out int index))
            return;

        // Block clicking on wrongly guessed faces
        if (_wronglyGuessed[index])
            return;

        if (_selectedGameIndex >= 0 && _selectedGameIndex < _gameFaces.Length)
        {
            Image prev = _gameFaces[_selectedGameIndex];
            prev.RenderTransform = Transform.Identity;

            if (_wronglyGuessed[_selectedGameIndex])
            {
                MarkFaceAsWrong(_selectedGameIndex);
            }
            else
            {
                prev.Opacity = _crossedOut[_selectedGameIndex] ? 0.5 : 1.0;
            }
        }

        if (_selectedGameIndex == index)
        {
            _selectedGameIndex = -1;
            return;
        }

        _selectedGameIndex = index;
        var scale = new ScaleTransform(1.08, 1.08, img.Width / 2, img.Height / 2);
        img.RenderTransform = scale;
        img.Opacity = 1.0;
    }

    private void GuessButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_gameStarted || _selectedGameIndex < 0)
            return;

        int guessIndex = _selectedGameIndex;
        _gameFaces[guessIndex].RenderTransform = Transform.Identity;
        _gameFaces[guessIndex].Opacity = _crossedOut[guessIndex] ? 0.5 : 1.0;
        _selectedGameIndex = -1;

        _ = SubmitFinalGuessAsync(guessIndex);
    }

    private void SelectButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_gameStarted || _selectedGameIndex < 0)
            return;

        int idx = _selectedGameIndex;
        _crossedOut[idx] = !_crossedOut[idx];

        if (_wronglyGuessed[idx])
        {
            MarkFaceAsWrong(idx);
        }
        else
        {
            _gameFaces[idx].Opacity = _crossedOut[idx] ? 0.5 : 1.0;
        }

        _gameFaces[idx].RenderTransform = Transform.Identity;
        _selectedGameIndex = -1;
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

                string enemyId = _config.Role == AppRole.Host
                    ? payload.ClientFaceId
                    : payload.HostFaceId;

                StartGame(myId, enemyId);
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
                {
                    ShowEndScreen(true);
                }
                else
                {
                    // Mark the wrongly guessed face with red filter
                    if (!string.IsNullOrWhiteSpace(payload.GuessedFaceId) 
                        && int.TryParse(payload.GuessedFaceId, out int faceId))
                    {
                        int wrongIndex = faceId - 1; // Face IDs are 1-based, arrays are 0-based
                        if (wrongIndex >= 0 && wrongIndex < _gameFaces.Length)
                        {
                            _wronglyGuessed[wrongIndex] = true;
                            MarkFaceAsWrong(wrongIndex);
                        }
                    }
                }
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

    private void MarkFaceAsWrong(int index)
    {
        if (index < 0 || index >= _gameFaces.Length)
            return;

        var wrongFace = _gameFaces[index];

        // Apply red tint only to existing (non-transparent) pixels by manipulating pixel data
        if (wrongFace.Source is BitmapSource originalBitmap)
        {
            int width = originalBitmap.PixelWidth;
            int height = originalBitmap.PixelHeight;
            int stride = width * 4; // 4 bytes per pixel (BGRA)
            byte[] pixels = new byte[height * stride];

            // Copy original pixels
            originalBitmap.CopyPixels(pixels, stride, 0);

            // Apply red tint to non-transparent pixels
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte alpha = pixels[i + 3];
                if (alpha > 0) // Only modify non-transparent pixels
                {
                    // Apply red overlay: blend with red color
                    // Original color * (1 - red_alpha) + red * red_alpha
                    float redAlpha = 0.35f; // Red overlay intensity
                    pixels[i + 0] = (byte)(pixels[i + 0] * (1 - redAlpha)); // Blue
                    pixels[i + 1] = (byte)(pixels[i + 1] * (1 - redAlpha)); // Green
                    pixels[i + 2] = (byte)(pixels[i + 2] * (1 - redAlpha) + 255 * redAlpha); // Red
                    // Alpha remains unchanged
                }
            }

            // Create new bitmap with modified pixels
            var modifiedBitmap = BitmapSource.Create(
                width, height,
                96, 96,
                PixelFormats.Bgra32,
                null,
                pixels,
                stride);

            wrongFace.Source = modifiedBitmap;
        }

        wrongFace.Opacity = 0.9;
        wrongFace.Cursor = Cursors.No;
    }

    private void ShowEndScreen(bool won)
    {
        _gameStarted = false;
        endBanner.Source = new BitmapImage(
            new Uri(won ? "/assets/baners/win.png" : "/assets/baners/lose.png", UriKind.Relative));

        // Show enemy character face
        if (!string.IsNullOrWhiteSpace(_enemyFaceId) && int.TryParse(_enemyFaceId, out int faceId))
        {
            int enemyIndex = faceId - 1; // Face IDs are 1-based, arrays are 0-based
            if (enemyIndex >= 0 && enemyIndex < _gameFaces.Length)
            {
                enemyFace.Source = _gameFaces[enemyIndex].Source;
            }
        }

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