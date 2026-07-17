using Carango.Domain;

namespace Carango.Application;

public class AutenticarVendedorService
{
    private readonly IVendedorRepository _repositorio;
    private readonly IPasswordHasher _hasher;
    private readonly IGeradorToken _geradorToken;

    public AutenticarVendedorService(IVendedorRepository repositorio, IPasswordHasher hasher, IGeradorToken geradorToken)
    {
        _repositorio = repositorio;
        _hasher = hasher;
        _geradorToken = geradorToken;
    }

    public async Task<(Vendedor Vendedor, TokenGerado Token)> AutenticarAsync(AutenticarVendedorInput input)
    {
        var email = input.Email.Trim();
        var vendedor = await _repositorio.ObterPorEmailAsync(email);

        // hash fictício sempre calculado, mesmo quando o e-mail não existe — sem isso, a rejeição de um
        // e-mail desconhecido é quase instantânea enquanto uma senha errada custa o tempo inteiro do
        // PBKDF2, um canal lateral de tempo que permite enumerar e-mails cadastrados mesmo com a
        // mensagem de erro idêntica (AC #2/FR-1 exige não revelar qual dado errou)
        var hashParaVerificar = vendedor?.SenhaHash ?? _hasher.Hash(input.Senha);
        var senhaValida = _hasher.Verificar(input.Senha, hashParaVerificar);

        if (vendedor is null || !senhaValida)
            throw new CredenciaisInvalidasException();

        var token = _geradorToken.Gerar(vendedor);

        return (vendedor, token);
    }
}
