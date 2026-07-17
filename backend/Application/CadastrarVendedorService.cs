using Carango.Domain;

namespace Carango.Application;

public class CadastrarVendedorService
{
    private readonly IVendedorRepository _repositorio;
    private readonly IPasswordHasher _hasher;

    public CadastrarVendedorService(IVendedorRepository repositorio, IPasswordHasher hasher)
    {
        _repositorio = repositorio;
        _hasher = hasher;
    }

    public async Task<Vendedor> CadastrarAsync(CadastrarVendedorInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Senha))
            throw new ArgumentException("Senha é obrigatória.", nameof(input.Senha));

        var email = input.Email.Trim();

        if (await _repositorio.ExisteEmailAsync(email))
            throw new EmailJaCadastradoException();

        var senhaHash = _hasher.Hash(input.Senha);
        var vendedor = new Vendedor(email, senhaHash, input.Tipo, input.Telefone, input.CnpjRazaoSocial);

        await _repositorio.AdicionarAsync(vendedor);

        return vendedor;
    }
}
