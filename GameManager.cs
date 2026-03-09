using GuessWho.Models;

namespace GuessWho;

/// <summary>
/// Zarządza kolekcją postaci w grze GuessWho.
/// Odpowiada za generowanie listy twarzy, losowe przypisanie postaci graczom oraz wyszukiwanie postaci po identyfikatorze.
/// </summary>
public sealed class GameManager
{
    private readonly List<Face> _faces = Enumerable.Range(1, 15)
        .Select(i => new Face(i.ToString()))
        .ToList();

    private readonly Random _random = new();

    /// <summary>
    /// Pobiera niemodyfikowalną listę wszystkich dostępnych postaci w grze.
    /// </summary>
    /// <returns>Kolekcja tylko do odczytu zawierająca 15 obiektów Face.</returns>
    public IReadOnlyList<Face> Faces => _faces;

    /// <summary>
    /// Generuje losowe przypisanie postaci dla hosta i klienta, zapewniając że są różne.
    /// </summary>
    /// <returns>Krotka zawierająca Face dla hosta oraz Face dla klienta.</returns>
    public (Face hostFace, Face clientFace) GenerateAssignments()
    {
        int hostIndex = _random.Next(_faces.Count);
        int clientIndex = GenerateDifferentIndex(hostIndex);

        return (_faces[hostIndex], _faces[clientIndex]);
    }

    /// <summary>
    /// Generuje losowy indeks różny od podanego.
    /// </summary>
    /// <param name="excludedIndex">Indeks który ma być wykluczony.</param>
    /// <returns>Losowy indeks różny od excludedIndex.</returns>
    private int GenerateDifferentIndex(int excludedIndex)
    {
        int index;
        do
        {
            index = _random.Next(_faces.Count);
        } while (index == excludedIndex);

        return index;
    }

    /// <summary>
    /// Wyszukuje postać po jej identyfikatorze (bez uwzględniania wielkości liter).
    /// </summary>
    /// <param name="id">Identyfikator postaci do znalezienia.</param>
    /// <returns>Obiekt Face jeśli znaleziono, w przeciwnym razie null.</returns>
    public Face? FindById(string id)
    {
        return _faces.FirstOrDefault(face => face.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }
}
