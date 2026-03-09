using System.Text.Json;

namespace GuessWho.Models;

/// <summary>
/// Definiuje stałe reprezentujące typy wiadomości w komunikacji sieciowej gry.
/// </summary>
public static class MessageTypes
{
    public const string CharacterReady = "CHARACTER_READY";
    public const string GameStart = "GAME_START";
    public const string FinalGuess = "FINAL_GUESS";
    public const string GuessResult = "GUESS_RESULT";
    public const string Rematch = "REMATCH";
}

/// <summary>
/// Bazowa wiadomość sieciowa zawierająca typ oraz payload w formacie JSON.
/// </summary>
public sealed class GameMessage
{
    public string Type { get; init; } = string.Empty;
    public JsonElement Payload { get; init; }
}

/// <summary>
/// Payload wiadomości rozpoczynającej grę, zawierający ID postaci przypisanych do hosta i klienta.
/// </summary>
public sealed class GameStartPayload
{
    public string HostFaceId { get; init; } = string.Empty;
    public string ClientFaceId { get; init; } = string.Empty;
}

/// <summary>
/// Payload wiadomości sygnalizującej gotowość gracza, zawierający ID wybranej postaci.
/// </summary>
public sealed class CharacterReadyPayload
{
    public string FaceId { get; init; } = string.Empty;
}

/// <summary>
/// Payload wiadomości z ostatecznym zgadywaniem, zawierający ID zgadywanej postaci.
/// </summary>
public sealed class FinalGuessPayload
{
    public string GuessedFaceId { get; init; } = string.Empty;
}

/// <summary>
/// Payload wiadomości z wynikiem zgadywania, zawierający informację czy zgadnięto poprawnie oraz ID zgadywanej postaci.
/// </summary>
public sealed class GuessResultPayload
{
    public bool Correct { get; init; }
    public string GuessedFaceId { get; init; } = string.Empty;
}

/// <summary>
/// Payload wiadomości prośby o rewanż.
/// </summary>
public sealed class RematchPayload
{
}
