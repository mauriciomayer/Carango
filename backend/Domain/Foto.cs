namespace Carango.Domain;

public class Foto
{
    public Guid Id { get; private set; }
    public string Url { get; private set; } = null!;
    public int Ordem { get; private set; }

    private Foto()
    {
        // EF Core
    }

    public Foto(string url, int ordem)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new ArgumentException("Url é obrigatória.", nameof(url));

        Id = Guid.NewGuid();
        Url = url;
        Ordem = ordem;
    }
}
