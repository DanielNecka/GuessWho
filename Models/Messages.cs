using System.Text.Json;

namespace GuessWho.Models;

public static class MessageTypes
{
    public const string CharacterReady = "CHARACTER_READY";
    public const string GameStart = "GAME_START";
    public const string FinalGuess = "FINAL_GUESS";
    public const string GuessResult = "GUESS_RESULT";
    public const string Rematch = "REMATCH";
}

public sealed class GameMessage
{
    public string Type { get; init; } = string.Empty;
    public JsonElement Payload { get; init; }
}

public sealed class GameStartPayload
{
    public string HostFaceId { get; init; } = string.Empty;
    public string ClientFaceId { get; init; } = string.Empty;
}

public sealed class CharacterReadyPayload
{
    public string FaceId { get; init; } = string.Empty;
}

public sealed class FinalGuessPayload
{
    public string GuessedFaceId { get; init; } = string.Empty;
}

public sealed class GuessResultPayload
{
    public bool Correct { get; init; }
    public string GuessedFaceId { get; init; } = string.Empty;
}

public sealed class RematchPayload
{
}
