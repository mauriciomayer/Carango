using Carango.Domain;

namespace Carango.Application;

public class AssinarPlanoLojistaService
{
    // valor provisório — mesmo modelo de placeholder já usado em ValorDestaque (Story 4.1),
    // modelo de cobrança real (Pergunta Aberta 3 do PRD) ainda não definido pelo cliente
    private const decimal ValorAssinatura = 49.90m;

    private readonly IVendedorRepository _vendedorRepositorio;
    private readonly IPlanoLojistaRepository _planoRepositorio;
    private readonly IBillingGateway _billingGateway;

    public AssinarPlanoLojistaService(
        IVendedorRepository vendedorRepositorio, IPlanoLojistaRepository planoRepositorio, IBillingGateway billingGateway)
    {
        _vendedorRepositorio = vendedorRepositorio;
        _planoRepositorio = planoRepositorio;
        _billingGateway = billingGateway;
    }

    public async Task<PlanoLojista> AssinarAsync(Guid vendedorId)
    {
        // vendedorId vem sempre das claims do token já validado (AD-9) — um Vendedor "não encontrado"
        // aqui seria um estado inconsistente entre o token emitido e o banco, não um caminho de erro
        // de negócio esperado como os outros lançados por este método
        var vendedor = await _vendedorRepositorio.ObterPorIdAsync(vendedorId)
            ?? throw new InvalidOperationException("Vendedor autenticado não encontrado.");

        if (vendedor.Tipo != TipoVendedor.Lojista)
            throw new VendedorNaoELojistaException();

        // checagem de idempotência ANTES de cobrar — mesmo achado de code review da Story 4.1
        // (DestacarAsync): nunca cobrar por uma transição que já aconteceu. Essa checagem sozinha não
        // fecha a janela de corrida entre duas chamadas simultâneas — PlanoLojistaRepository.AdicionarAsync
        // traduz a violação do índice único em VendedorId pro mesmo PlanoLojistaJaAtivoException, mesmo
        // padrão de defesa em profundidade já usado em AnuncioRepository/VendedorRepository
        if (await _planoRepositorio.TemPlanoAtivoAsync(vendedorId))
            throw new PlanoLojistaJaAtivoException();

        var resultado = await _billingGateway.CobrarAsync(vendedorId, "Assinatura Plano Lojista", ValorAssinatura);
        if (!resultado.Sucesso)
            throw new CobrancaFalhouException(resultado.MotivoFalha ?? "Pagamento não autorizado.");

        var plano = PlanoLojista.Assinar(vendedorId);
        await _planoRepositorio.AdicionarAsync(plano);

        return plano;
    }
}
