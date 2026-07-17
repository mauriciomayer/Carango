using Carango.Domain;

namespace Carango.Application;

// separado de AssinarPlanoLojistaService de propósito — mesma relação que GerenciarAnuncioService
// tem com CriarAnuncioService (um cria, o outro gerencia o que já existe), evita que o serviço de
// criação acumule responsabilidades não relacionadas
public class GerenciarPlanoLojistaService
{
    private readonly IPlanoLojistaRepository _planoRepositorio;

    public GerenciarPlanoLojistaService(IPlanoLojistaRepository planoRepositorio)
    {
        _planoRepositorio = planoRepositorio;
    }

    // null é um resultado válido aqui (Vendedor nunca assinou), não um erro — quem chama decide
    // como exibir isso (Task 4: 404 sem Problem(), Task 5: mensagem calma "sem plano ativo")
    public Task<PlanoLojista?> ObterAsync(Guid vendedorId) =>
        _planoRepositorio.ObterPorVendedorAsync(vendedorId);

    public async Task<PlanoLojista> CancelarAsync(Guid vendedorId)
    {
        var plano = await _planoRepositorio.ObterPorVendedorAsync(vendedorId)
            ?? throw new PlanoLojistaNaoEncontradoException();

        // Cancelar() guarda Status == Ativo e lança InvalidOperationException caso contrário —
        // deixado subir sem captura aqui, mesmo padrão de GerenciarAnuncioService pras transições
        // de Anuncio (ex.: Pausar() num Anúncio não-Ativo)
        plano.Cancelar();
        await _planoRepositorio.AtualizarAsync(plano);

        return plano;
    }
}
