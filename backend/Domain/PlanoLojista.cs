namespace Carango.Domain;

public class PlanoLojista
{
    public Guid Id { get; private set; }
    public Guid VendedorId { get; private set; }
    public StatusPlanoLojista Status { get; private set; }

    private PlanoLojista()
    {
        // EF Core
    }

    private PlanoLojista(Guid vendedorId)
    {
        if (vendedorId == Guid.Empty)
            throw new ArgumentException("VendedorId é obrigatório.", nameof(vendedorId));

        Id = Guid.NewGuid();
        VendedorId = vendedorId;
        Status = StatusPlanoLojista.Ativo;
    }

    public static PlanoLojista Assinar(Guid vendedorId) => new(vendedorId);

    public void Cancelar()
    {
        if (Status != StatusPlanoLojista.Ativo)
            throw new InvalidOperationException($"Só é possível cancelar um Plano Lojista a partir do status Ativo (status atual: {Status}).");

        Status = StatusPlanoLojista.Cancelado;
    }
}
