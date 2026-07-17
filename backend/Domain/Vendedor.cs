namespace Carango.Domain;

public class Vendedor
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = null!;
    public string? Telefone { get; private set; }
    public string SenhaHash { get; private set; } = null!;
    public TipoVendedor Tipo { get; private set; }
    public string? CnpjRazaoSocial { get; private set; }

    private Vendedor()
    {
        // EF Core
    }

    public Vendedor(string email, string senhaHash, TipoVendedor tipo, string? telefone = null, string? cnpjRazaoSocial = null)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email é obrigatório.", nameof(email));
        if (string.IsNullOrWhiteSpace(senhaHash))
            throw new ArgumentException("Senha é obrigatória.", nameof(senhaHash));
        if (!Enum.IsDefined(tipo))
            throw new ArgumentException("Tipo de Vendedor inválido.", nameof(tipo));
        if (tipo == TipoVendedor.Lojista && string.IsNullOrWhiteSpace(cnpjRazaoSocial))
            throw new ArgumentException("CNPJ/razão social é obrigatório para Vendedor do tipo Lojista.", nameof(cnpjRazaoSocial));

        Id = Guid.NewGuid();
        Email = email;
        Telefone = telefone;
        SenhaHash = senhaHash;
        Tipo = tipo;
        CnpjRazaoSocial = cnpjRazaoSocial;
    }
}
