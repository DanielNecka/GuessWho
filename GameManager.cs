using GuessWho.Models;

namespace GuessWho;

public sealed class GameManager
{
    private readonly List<Face> _faces = Enumerable.Range(1, 15)
        .Select(i => new Face(i.ToString()))
        .ToList();

    private readonly Random _random = new();

    public IReadOnlyList<Face> Faces => _faces;

    public (Face hostFace, Face clientFace) GenerateAssignments()
    {
        int hostIndex = _random.Next(_faces.Count);
        int clientIndex;
        do
        {
            clientIndex = _random.Next(_faces.Count);
        } while (clientIndex == hostIndex);

        return (_faces[hostIndex], _faces[clientIndex]);
    }

    public Face? FindById(string id)
    {
        return _faces.FirstOrDefault(face => face.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }
}
