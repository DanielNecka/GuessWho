using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GuessWho.Models;

namespace GuessWho;

/// <summary>
/// Główne okno aplikacji GuessWho obsługujące interfejs użytkownika i logikę gry.
/// Zarządza ekranami, połączeniem sieciowym, wyborem postaci oraz rozgrywką.
/// </summary>
public partial class MainWindow : Window
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly GameManager _gameManager = new();
    private NetworkManager? _networkManager;
    private AppConfig? _config;
    private CancellationTokenSource? _connectCts;
    private LogWindow? _logWindow;

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
    private bool _isMyTurn;

    private readonly Image[] _selectFaces;
    private readonly Image[] _gameFaces;

    /// <summary>
    /// Inicjalizuje główne okno i przypisuje tablice kontrolek obrazów.
    /// </summary>
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

        _logWindow = new LogWindow();
        AppLogger.Log("Aplikacja GuessWho uruchomiona.");
        LogLocalNetworkInfo();
    }

    /// <summary>
    /// Loguje informacje o lokalnych adresach sieciowych.
    /// </summary>
    private static void LogLocalNetworkInfo()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ips = host.AddressList
                .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                .Select(a => a.ToString());
            AppLogger.Log($"Lokalne adresy IP: {string.Join(", ", ips)}");
        }
        catch { }
    }

    /// <summary>
    /// Przełącza widoczność ekranów w aplikacji.
    /// </summary>
    /// <param name="screen">Nazwa ekranu do pokazania: "start", "select", "game" lub "end".</param>
    private void ShowScreen(string screen)
    {
        HideAllScreens();
        ShowSpecificScreen(screen);
    }

    /// <summary>
    /// Ukrywa wszystkie ekrany aplikacji.
    /// </summary>
    private void HideAllScreens()
    {
        startScreen.Visibility = Visibility.Collapsed;
        selectScreen.Visibility = Visibility.Collapsed;
        gameScreen.Visibility = Visibility.Collapsed;
        endScreen.Visibility = Visibility.Collapsed;
    }

    /// <summary>
    /// Pokazuje określony ekran.
    /// </summary>
    /// <param name="screen">Nazwa ekranu do pokazania.</param>
    private void ShowSpecificScreen(string screen)
    {
        switch (screen)
        {
            case "start": startScreen.Visibility = Visibility.Visible; break;
            case "select": selectScreen.Visibility = Visibility.Visible; break;
            case "game": gameScreen.Visibility = Visibility.Visible; break;
            case "end": endScreen.Visibility = Visibility.Visible; break;
        }
    }

    /// <summary>
    /// Obsługuje kliknięcie przycisku Host - rozpoczyna połączenie jako host.
    /// </summary>
    private async void HostButton_Click(object sender, MouseButtonEventArgs e)
    {
        await StartConnectionAsync(AppRole.Host);
    }

    /// <summary>
    /// Obsługuje kliknięcie przycisku Client - rozpoczyna połączenie jako klient.
    /// </summary>
    private async void ClientButton_Click(object sender, MouseButtonEventArgs e)
    {
        await StartConnectionAsync(AppRole.Client);
    }

    /// <summary>
    /// Rozpoczyna połączenie sieciowe w określonej roli.
    /// </summary>
    /// <param name="role">Rola w grze (Host lub Client).</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task StartConnectionAsync(AppRole role)
    {
        CleanupPreviousConnection();

        (string hostIp, int port) = AppConfig.LoadNetworkSettings();
        _config = new AppConfig { Role = role, HostIp = hostIp, Port = port };
        _networkManager = new NetworkManager(_config);
        _networkManager.MessageReceived += OnMessageReceived;
        _connectCts = new CancellationTokenSource();
        AppLogger.Log($"Łączenie jako {role}...");

        try
        {
            await _networkManager.StartAsync(_connectCts.Token);
            AppLogger.Log("Połączenie nawiazane. Ekran wyboru postaci.");
            ResetSelectScreen();
            ShowScreen("select");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            AppLogger.Log($"Błąd połączenia: {ex.Message}");
            ShowConnectionError(ex);
        }
    }

    /// <summary>
    /// Czyści poprzednie połączenie sieciowe i anuluje tokeny.
    /// </summary>
    private void CleanupPreviousConnection()
    {
        _connectCts?.Cancel();
        _networkManager?.Dispose();
    }

    /// <summary>
    /// Wyświetla okno dialogowe z błędem połączenia.
    /// </summary>
    /// <param name="ex">Wyjątek zawierający szczegóły błędu.</param>
    private void ShowConnectionError(Exception ex)
    {
        MessageBox.Show(
            $"Nie udało się połączyć: {ex.Message}\n\n" +
            $"Sprawdź config.txt — host_ip musi wskazywać IP hosta.\n" +
            $"Aktualnie: {_config?.HostIp}:{_config?.Port}",
            "Błąd połączenia", MessageBoxButton.OK, MessageBoxImage.Warning);
        ShowScreen("start");
    }

    /// <summary>
    /// Resetuje ekran wyboru postaci do stanu początkowego.
    /// </summary>
    private void ResetSelectScreen()
    {
        _selectedCharacterIndex = -1;
        _localReady = false;
        _hostFaceId = null;
        _clientFaceId = null;

        ResetAllSelectFaces();
    }

    /// <summary>
    /// Resetuje wygląd wszystkich obrazków postaci na ekranie wyboru.
    /// </summary>
    private void ResetAllSelectFaces()
    {
        foreach (Image face in _selectFaces)
        {
            face.Opacity = 1.0;
            face.RenderTransform = Transform.Identity;
        }
    }

    /// <summary>
    /// Obsługuje kliknięcie na postać na ekranie wyboru - zaznacza lub odznacza postać.
    /// </summary>
    private void SelectFace_Click(object sender, MouseButtonEventArgs e)
    {
        if (!TryGetImageIndex(sender, out int index))
            return;

        DeselectPreviousFace();

        if (_selectedCharacterIndex == index)
        {
            _selectedCharacterIndex = -1;
            return;
        }

        SelectNewFace(index);
    }

    /// <summary>
    /// Próbuje pobrać indeks z kontrolki Image.
    /// </summary>
    /// <param name="sender">Obiekt źródłowy eventu.</param>
    /// <param name="index">Wyjściowy indeks jeśli udało się sparsować.</param>
    /// <returns>True jeśli udało się pobrać indeks, false w przeciwnym razie.</returns>
    private bool TryGetImageIndex(object sender, out int index)
    {
        index = -1;
        return sender is Image img 
            && img.Tag is string tag 
            && int.TryParse(tag, out index);
    }

    /// <summary>
    /// Odznacza poprzednio wybraną postać.
    /// </summary>
    private void DeselectPreviousFace()
    {
        if (_selectedCharacterIndex >= 0 && _selectedCharacterIndex < _selectFaces.Length)
        {
            _selectFaces[_selectedCharacterIndex].RenderTransform = Transform.Identity;
            _selectFaces[_selectedCharacterIndex].Opacity = 1.0;
        }
    }

    /// <summary>
    /// Zaznacza nową postać przez powiększenie i aktualizację indeksu.
    /// </summary>
    /// <param name="index">Indeks postaci do zaznaczenia.</param>
    private void SelectNewFace(int index)
    {
        _selectedCharacterIndex = index;
        Image img = _selectFaces[index];
        var scale = new ScaleTransform(1.08, 1.08, img.Width / 2, img.Height / 2);
        img.RenderTransform = scale;
        img.Opacity = 1.0;
    }

    /// <summary>
    /// Obsługuje kliknięcie przycisku Play - potwierdza wybór postaci i informuje przeciwnika.
    /// </summary>
    private async void PlayButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!CanConfirmCharacter())
            return;

        await ConfirmCharacterSelectionAsync();
    }

    /// <summary>
    /// Sprawdza czy możliwe jest potwierdzenie wyboru postaci.
    /// </summary>
    /// <returns>True jeśli postać jest wybrana, połączenie aktywne i gracz nie jest jeszcze gotowy.</returns>
    private bool CanConfirmCharacter()
    {
        return _selectedCharacterIndex >= 0 
            && _networkManager is not null 
            && _config is not null 
            && !_localReady;
    }

    /// <summary>
    /// Potwierdza wybór postaci i wysyła informację do przeciwnika.
    /// </summary>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task ConfirmCharacterSelectionAsync()
    {
        _localReady = true;
        string selectedFaceId = _gameManager.Faces[_selectedCharacterIndex].Id;
        AppLogger.Log($"Wybrano postać: {selectedFaceId}");

        if (_config!.Role == AppRole.Host)
            _hostFaceId = selectedFaceId;
        else
            _clientFaceId = selectedFaceId;

        await _networkManager!.SendAsync(
            MessageTypes.CharacterReady,
            new CharacterReadyPayload { FaceId = selectedFaceId });

        if (_config.Role == AppRole.Host)
            await TryStartGameAsHostAsync();
    }

    /// <summary>
    /// Próbuje rozpocząć grę jeśli host ma komplet informacji o wyborach obu graczy.
    /// </summary>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task TryStartGameAsHostAsync()
    {
        if (!CanStartGame())
            return;

        await SendGameStartMessageAsync();
        StartGame(_hostFaceId!, _clientFaceId!);
    }

    /// <summary>
    /// Sprawdza czy możliwe jest rozpoczęcie gry.
    /// </summary>
    /// <returns>True jeśli host ma wybory obu graczy.</returns>
    private bool CanStartGame()
    {
        return _networkManager is not null 
            && _localReady
            && !string.IsNullOrWhiteSpace(_hostFaceId)
            && !string.IsNullOrWhiteSpace(_clientFaceId);
    }

    /// <summary>
    /// Wysyła wiadomość rozpoczęcia gry do klienta.
    /// </summary>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task SendGameStartMessageAsync()
    {
        await _networkManager!.SendAsync(
            MessageTypes.GameStart,
            new GameStartPayload
            {
                HostFaceId = _hostFaceId!,
                ClientFaceId = _clientFaceId!
            });
    }

    /// <summary>
    /// Rozpoczyna grę - inicjalizuje stan gry i przełącza na ekran rozgrywki.
    /// </summary>
    /// <param name="myFaceId">ID postaci gracza.</param>
    /// <param name="enemyFaceId">ID postaci przeciwnika.</param>
    private void StartGame(string myFaceId, string enemyFaceId)
    {
        AppLogger.Log($"Gra rozpoczęta! Moja postać: {myFaceId}, przeciwnik: {enemyFaceId}");
        InitializeGameState(myFaceId, enemyFaceId);
        ResetGameFaces();
        SetPlayerFaceImage();
        UpdateTurnBanner();
        ShowScreen("game");
    }

    /// <summary>
    /// Inicjalizuje zmienne stanu gry.
    /// </summary>
    /// <param name="myFaceId">ID postaci gracza.</param>
    /// <param name="enemyFaceId">ID postaci przeciwnika.</param>
    private void InitializeGameState(string myFaceId, string enemyFaceId)
    {
        _myFaceId = myFaceId;
        _enemyFaceId = enemyFaceId;
        _gameStarted = true;
        _selectedGameIndex = -1;
        _isMyTurn = _config?.Role == AppRole.Host;
    }

    /// <summary>
    /// Resetuje wygląd i stan wszystkich postaci na ekranie gry.
    /// </summary>
    private void ResetGameFaces()
    {
        string[] faceNames =
        [
            "NataliaKoczkodaj", "DamianRospenowski", "EwaKuczera", "JacekRusin",
            "KarolinaKlabis", "KamilWiniarczyk", "KarolinaSuch", "BartoszPac",
            "AngelikaZelek", "MateuszGawlik", "AnnaGrabowska", "PiotrKania",
            "AnnaWozniak", "MateuszSzarek", "JolantaTargosz"
        ];

        for (int i = 0; i < _crossedOut.Length; i++)
        {
            _crossedOut[i] = false;
            _wronglyGuessed[i] = false;
            _gameFaces[i].Opacity = 1.0;
            _gameFaces[i].RenderTransform = Transform.Identity;
            _gameFaces[i].Effect = null;
            _gameFaces[i].Cursor = Cursors.Hand;
            _gameFaces[i].Source = new BitmapImage(
                new Uri($"/assets/images/{faceNames[i]}.png", UriKind.Relative));
        }
    }

    /// <summary>
    /// Ustawia obraz postaci gracza na podstawie wybranej postaci.
    /// </summary>
    private void SetPlayerFaceImage()
    {
        if (_selectedCharacterIndex >= 0 && _selectedCharacterIndex < _selectFaces.Length)
        {
            var selectedFaceSource = _selectFaces[_selectedCharacterIndex].Source;
            playerFace.Source = selectedFaceSource;
        }
    }

    /// <summary>
    /// Obsługuje kliknięcie na postać w trakcie gry - zaznacza lub odznacza postać.
    /// </summary>
    private void GameFace_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_gameStarted || !_isMyTurn || !TryGetImageIndex(sender, out int index))
            return;

        if (_wronglyGuessed[index])
            return;

        DeselectPreviousGameFace();

        if (_selectedGameIndex == index)
        {
            _selectedGameIndex = -1;
            return;
        }

        SelectNewGameFace(index);
    }

    /// <summary>
    /// Odznacza poprzednio wybraną postać w grze.
    /// </summary>
    private void DeselectPreviousGameFace()
    {
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
    }

    /// <summary>
    /// Zaznacza nową postać w grze przez powiększenie.
    /// </summary>
    /// <param name="index">Indeks postaci do zaznaczenia.</param>
    private void SelectNewGameFace(int index)
    {
        _selectedGameIndex = index;
        Image img = _gameFaces[index];
        var scale = new ScaleTransform(1.08, 1.08, img.Width / 2, img.Height / 2);
        img.RenderTransform = scale;
        img.Opacity = 1.0;
    }

    /// <summary>
    /// Obsługuje kliknięcie przycisku Guess - wysyła ostateczne zgadywanie do przeciwnika.
    /// </summary>
    private void GuessButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_gameStarted || !_isMyTurn || _selectedGameIndex < 0)
            return;

        int guessIndex = _selectedGameIndex;
        ResetGuessedFaceAppearance(guessIndex);
        _selectedGameIndex = -1;

        _ = SubmitFinalGuessAsync(guessIndex);
    }

    /// <summary>
    /// Resetuje wygląd zgadywanej postaci.
    /// </summary>
    /// <param name="guessIndex">Indeks postaci.</param>
    private void ResetGuessedFaceAppearance(int guessIndex)
    {
        _gameFaces[guessIndex].RenderTransform = Transform.Identity;
        _gameFaces[guessIndex].Opacity = _crossedOut[guessIndex] ? 0.5 : 1.0;
    }

    /// <summary>
    /// Obsługuje kliknięcie przycisku Select - przełącza stan skreślenia postaci.
    /// </summary>
    private void SelectButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (!_gameStarted || !_isMyTurn || _selectedGameIndex < 0)
            return;

        int idx = _selectedGameIndex;
        ToggleFaceCrossedOut(idx);
        _selectedGameIndex = -1;
    }

    /// <summary>
    /// Przełącza stan skreślenia postaci i aktualizuje jej wygląd.
    /// </summary>
    /// <param name="idx">Indeks postaci.</param>
    private void ToggleFaceCrossedOut(int idx)
    {
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
    }

    /// <summary>
    /// Wysyła ostateczne zgadywanie do przeciwnika przez sieć.
    /// </summary>
    /// <param name="faceIndex">Indeks zgadywanej postaci.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task SubmitFinalGuessAsync(int faceIndex)
    {
        if (_networkManager is null || faceIndex < 0 || faceIndex >= _gameManager.Faces.Count)
            return;

        string guessedId = _gameManager.Faces[faceIndex].Id;
        AppLogger.Log($"Zgaduję postać: {guessedId}");

        await _networkManager.SendAsync(
            MessageTypes.FinalGuess,
            new FinalGuessPayload { GuessedFaceId = guessedId });

        _isMyTurn = false;
        UpdateTurnBanner();
    }

    /// <summary>
    /// Callback wywoływany po otrzymaniu wiadomości sieciowej - deleguje do wątku UI.
    /// </summary>
    /// <param name="message">Otrzymana wiadomość.</param>
    private void OnMessageReceived(GameMessage message)
    {
        _ = Dispatcher.InvokeAsync(async () => await HandleMessageAsync(message));
    }

    /// <summary>
    /// Obsługuje różne typy wiadomości sieciowych i wykonuje odpowiednie akcje.
    /// </summary>
    /// <param name="message">Wiadomość do przetworzenia.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task HandleMessageAsync(GameMessage message)
    {
        switch (message.Type)
        {
            case MessageTypes.CharacterReady:
                await HandleCharacterReadyAsync(message);
                break;
            case MessageTypes.GameStart:
                HandleGameStart(message);
                break;
            case MessageTypes.FinalGuess:
                await HandleFinalGuessAsync(message);
                break;
            case MessageTypes.GuessResult:
                HandleGuessResult(message);
                break;
            case MessageTypes.Rematch:
                HandleRematch();
                break;
        }
    }

    /// <summary>
    /// Obsługuje wiadomość o gotowości postaci klienta (tylko host).
    /// </summary>
    /// <param name="message">Wiadomość z payloadem CharacterReadyPayload.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task HandleCharacterReadyAsync(GameMessage message)
    {
        CharacterReadyPayload? payload = message.Payload.Deserialize<CharacterReadyPayload>(_jsonOptions);
        if (payload is null || _config?.Role != AppRole.Host)
            return;

        _clientFaceId = payload.FaceId;
        await TryStartGameAsHostAsync();
    }

    /// <summary>
    /// Obsługuje wiadomość rozpoczęcia gry - uruchamia grę z przypisanymi postaciami.
    /// </summary>
    /// <param name="message">Wiadomość z payloadem GameStartPayload.</param>
    private void HandleGameStart(GameMessage message)
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
    }

    /// <summary>
    /// Obsługuje wiadomość zgadywania - sprawdza poprawność i wysyła wynik.
    /// </summary>
    /// <param name="message">Wiadomość z payloadem FinalGuessPayload.</param>
    /// <returns>Task reprezentujący operację asynchroniczną.</returns>
    private async Task HandleFinalGuessAsync(GameMessage message)
    {
        FinalGuessPayload? payload = message.Payload.Deserialize<FinalGuessPayload>(_jsonOptions);
        if (payload is null || _networkManager is null)
            return;

        bool correct = payload.GuessedFaceId.Equals(_myFaceId, StringComparison.OrdinalIgnoreCase);
        AppLogger.Log($"Przeciwnik zgaduje: {payload.GuessedFaceId} — {(correct ? "TRAFIONY" : "PUDŁO")}");

        await _networkManager.SendAsync(
            MessageTypes.GuessResult,
            new GuessResultPayload
            {
                Correct = correct,
                GuessedFaceId = payload.GuessedFaceId
            });

        if (correct)
        {
            ShowEndScreen(false);
        }
        else
        {
            _isMyTurn = true;
            UpdateTurnBanner();
        }
    }

    /// <summary>
    /// Obsługuje wynik zgadywania - wyświetla zwycięstwo lub oznacza błędnie zgadniętą postać.
    /// </summary>
    /// <param name="message">Wiadomość z payloadem GuessResultPayload.</param>
    private void HandleGuessResult(GameMessage message)
    {
        GuessResultPayload? payload = message.Payload.Deserialize<GuessResultPayload>(_jsonOptions);
        if (payload is null)
            return;

        AppLogger.Log(payload.Correct
            ? "Wynik: WYGRANA!"
            : $"Wynik: pudło ({payload.GuessedFaceId})");

        if (payload.Correct)
        {
            ShowEndScreen(true);
        }
        else
        {
            MarkWronglyGuessedFace(payload.GuessedFaceId);
        }
    }

    /// <summary>
    /// Oznacza błędnie zgadniętą postać czerwonym odcieniem.
    /// </summary>
    /// <param name="guessedFaceId">ID błędnie zgadniętej postaci.</param>
    private void MarkWronglyGuessedFace(string guessedFaceId)
    {
        if (string.IsNullOrWhiteSpace(guessedFaceId) 
            || !int.TryParse(guessedFaceId, out int faceId))
            return;

        int wrongIndex = faceId - 1;
        if (wrongIndex >= 0 && wrongIndex < _gameFaces.Length)
        {
            _wronglyGuessed[wrongIndex] = true;
            MarkFaceAsWrong(wrongIndex);
        }
    }

    /// <summary>
    /// Obsługuje prośbę o rewanż - resetuje ekran wyboru postaci.
    /// </summary>
    private void HandleRematch()
    {
        AppLogger.Log("Przeciwnik chce rewanż.");
        ResetSelectScreen();
        ShowScreen("select");
    }

    /// <summary>
    /// Oznacza postać jako błędnie zgadniętą przez zastosowanie czerwonego odcienia na pikselach.
    /// </summary>
    /// <param name="index">Indeks postaci do oznaczenia.</param>
    private void MarkFaceAsWrong(int index)
    {
        if (index < 0 || index >= _gameFaces.Length)
            return;

        var wrongFace = _gameFaces[index];

        if (wrongFace.Source is BitmapSource originalBitmap)
        {
            BitmapSource modifiedBitmap = ApplyRedTintToBitmap(originalBitmap);
            wrongFace.Source = modifiedBitmap;
        }

        wrongFace.Opacity = 0.9;
        wrongFace.Cursor = Cursors.No;
    }

    /// <summary>
    /// Aplikuje czerwony odcień na nieprzezroczyste piksele bitmapy.
    /// </summary>
    /// <param name="originalBitmap">Oryginalna bitmapa.</param>
    /// <returns>Zmodyfikowana bitmapa z czerwonym odcieniem.</returns>
    private BitmapSource ApplyRedTintToBitmap(BitmapSource originalBitmap)
    {
        int width = originalBitmap.PixelWidth;
        int height = originalBitmap.PixelHeight;
        int stride = width * 4;
        byte[] pixels = new byte[height * stride];

        originalBitmap.CopyPixels(pixels, stride, 0);
        ApplyRedTintToPixels(pixels);

        return BitmapSource.Create(
            width, height,
            96, 96,
            PixelFormats.Bgra32,
            null,
            pixels,
            stride);
    }

    /// <summary>
    /// Aplikuje czerwony odcień na tablicę pikseli (format BGRA).
    /// </summary>
    /// <param name="pixels">Tablica pikseli do modyfikacji.</param>
    private void ApplyRedTintToPixels(byte[] pixels)
    {
        const float redAlpha = 0.35f;

        for (int i = 0; i < pixels.Length; i += 4)
        {
            byte alpha = pixels[i + 3];
            if (alpha > 0)
            {
                pixels[i + 0] = (byte)(pixels[i + 0] * (1 - redAlpha));
                pixels[i + 1] = (byte)(pixels[i + 1] * (1 - redAlpha));
                pixels[i + 2] = (byte)(pixels[i + 2] * (1 - redAlpha) + 255 * redAlpha);
            }
        }
    }

    /// <summary>
    /// Wyświetla ekran końcowy z banem wygranej/przegranej i postacią przeciwnika.
    /// </summary>
    /// <param name="won">True jeśli gracz wygrał, false jeśli przegrał.</param>
    private void ShowEndScreen(bool won)
    {
        AppLogger.Log(won ? "KONIEC — Wygrałeś!" : "KONIEC — Przegrałeś.");
        _gameStarted = false;
        SetEndBanner(won);
        SetEnemyFaceImage();
        ShowScreen("end");
    }

    /// <summary>
    /// Ustawia baner wygranej lub przegranej.
    /// </summary>
    /// <param name="won">True dla wygranej, false dla przegranej.</param>
    private void SetEndBanner(bool won)
    {
        endBanner.Source = new BitmapImage(
            new Uri(won ? "/assets/baners/win.png" : "/assets/baners/lose.png", UriKind.Relative));
    }

    /// <summary>
    /// Ustawia obraz postaci przeciwnika na ekranie końcowym.
    /// </summary>
    private void SetEnemyFaceImage()
    {
        if (string.IsNullOrWhiteSpace(_enemyFaceId) 
            || !int.TryParse(_enemyFaceId, out int faceId))
            return;

        int enemyIndex = faceId - 1;
        if (enemyIndex >= 0 && enemyIndex < _gameFaces.Length)
        {
            enemyFace.Source = _gameFaces[enemyIndex].Source;
        }
    }

    /// <summary>
    /// Obsługuje kliknięcie przycisku Play Again - wysyła prośbę o rewanż i resetuje ekran wyboru.
    /// </summary>
    private async void PlayAgainButton_Click(object sender, MouseButtonEventArgs e)
    {
        if (_networkManager is null)
            return;

        AppLogger.Log("Wysłano prośbę o rewanż.");
        await _networkManager.SendAsync(MessageTypes.Rematch, new RematchPayload());
        ResetSelectScreen();
        ShowScreen("select");
    }

    /// <summary>
    /// Obsługuje zamknięcie okna - czyści połączenie sieciowe i zasoby.
    /// </summary>
    /// <summary>
    /// Aktualizuje baner pokazujący czyja jest tura.
    /// </summary>
    private void UpdateTurnBanner()
    {
        if (!_gameStarted)
            return;

        string bannerPath = _isMyTurn 
            ? "/assets/baners/playerTurn.png" 
            : "/assets/baners/enemyTurn.png";

        var turnBanner = FindName("turnBanner") as Image;
        if (turnBanner != null)
        {
            turnBanner.Source = new BitmapImage(new Uri(bannerPath, UriKind.Relative));
        }

        UpdateGameControls();
    }

    /// <summary>
    /// Aktualizuje stan kontrolek gry w zależności od tury.
    /// </summary>
    private void UpdateGameControls()
    {
        if (_isMyTurn)
        {
            EnableGameControls();
        }
        else
        {
            DisableGameControls();
        }
    }

    /// <summary>
    /// Włącza kontrolki gry - pozwala na interakcję.
    /// </summary>
    private void EnableGameControls()
    {
        for (int i = 0; i < _gameFaces.Length; i++)
        {
            if (!_wronglyGuessed[i])
            {
                _gameFaces[i].Cursor = Cursors.Hand;
                _gameFaces[i].Opacity = _crossedOut[i] ? 0.5 : 1.0;
            }
        }

        var guessButton = FindName("guessButton") as Image;
        var selectButton = FindName("selectButton") as Image;

        if (guessButton != null)
        {
            guessButton.Cursor = Cursors.Hand;
            guessButton.Opacity = 1.0;
        }

        if (selectButton != null)
        {
            selectButton.Cursor = Cursors.Hand;
            selectButton.Opacity = 1.0;
        }
    }

    /// <summary>
    /// Wyłącza kontrolki gry - blokuje interakcję.
    /// </summary>
    private void DisableGameControls()
    {
        if (_selectedGameIndex >= 0 && _selectedGameIndex < _gameFaces.Length)
        {
            _gameFaces[_selectedGameIndex].RenderTransform = Transform.Identity;
            _selectedGameIndex = -1;
        }

        for (int i = 0; i < _gameFaces.Length; i++)
        {
            if (!_wronglyGuessed[i])
            {
                _gameFaces[i].Cursor = Cursors.No;
                _gameFaces[i].Opacity = _crossedOut[i] ? 0.3 : 0.6;
            }
        }

        var guessButton = FindName("guessButton") as Image;
        var selectButton = FindName("selectButton") as Image;

        if (guessButton != null)
        {
            guessButton.Cursor = Cursors.No;
            guessButton.Opacity = 0.5;
        }

        if (selectButton != null)
        {
            selectButton.Cursor = Cursors.No;
            selectButton.Opacity = 0.5;
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F12 && _logWindow is not null)
        {
            if (_logWindow.IsVisible)
                _logWindow.Hide();
            else
            {
                _logWindow.Show();
                Activate();
            }
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _logWindow?.Close();
        _connectCts?.Cancel();
        _networkManager?.Dispose();
        base.OnClosed(e);
    }
}