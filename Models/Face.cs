namespace GuessWho.Models;

/// <summary>
/// Reprezentuje pojedynczą postać w grze GuessWho.
/// Immutable record przechowujący unikalny identyfikator postaci.
/// </summary>
/// <param name="Id">Unikalny identyfikator postaci w formacie string.</param>
public sealed record Face(string Id);
