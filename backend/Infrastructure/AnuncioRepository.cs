using Carango.Application;
using Carango.Domain;
using Microsoft.EntityFrameworkCore;

namespace Carango.Infrastructure;

public class AnuncioRepository : IAnuncioRepository
{
    // limita o número de tokens da busca textual livre processados por requisição — achado no
    // code review: sem isso, um termo com centenas de palavras gera uma query com centenas de
    // Where/OR encadeados contra um endpoint público sem autenticação
    private const int MaxTokensTermoLivre = 10;

    // Story 3.4 — primeira story de paginação do projeto; estabelece offset (pagina + tamanho
    // fixo) como o estilo, não cursor (ver Dev Notes da story). O frontend infere fim de lista
    // quando a resposta vem com menos itens que esta constante, sem precisar de envelope na resposta
    private const int TamanhoPagina = 20;

    private readonly CarangoDbContext _dbContext;
    private readonly IRankingService _rankingService;

    public AnuncioRepository(CarangoDbContext dbContext, IRankingService rankingService)
    {
        _dbContext = dbContext;
        _rankingService = rankingService;
    }

    // sem tradução de DbUpdateException pra LimiteDeAnunciosAtivosExcedidoException aqui — a Story
    // 4.2 removeu o índice único VendedorIdSeAtivo (AnuncioConfiguration) que garantia essa invariante
    // no banco (MySQL não permite índice condicionado ao status de outra tabela, PlanosLojista). A
    // proteção contra corrida entre publicações concorrentes do mesmo Vendedor existe só na checagem
    // de CriarAnuncioService agora — trade-off documentado no Dev Notes da Story 4.2
    public async Task AdicionarAsync(Anuncio anuncio)
    {
        _dbContext.Anuncios.Add(anuncio);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<int> ContarAtivosPorVendedorAsync(Guid vendedorId) =>
        await _dbContext.Anuncios.CountAsync(a => a.VendedorId == vendedorId && a.Status == StatusAnuncio.Ativo);

    // sem .AsNoTracking() — diferente da leitura pura da Story 1.3 (VendedorRepository.ObterPorEmailAsync),
    // porque aqui a entidade retornada é a mesma que AtualizarAsync vai salvar no mesmo DbContext por requisição
    public async Task<Anuncio?> ObterPorIdAsync(Guid id) =>
        await _dbContext.Anuncios.Include(a => a.Fotos).FirstOrDefaultAsync(a => a.Id == id);

    // nenhum Update()/Attach() explícito é necessário — a entidade já está rastreada desde que veio
    // de ObterPorIdAsync no mesmo DbContext. Sem tradução de DbUpdateException aqui pelo mesmo motivo
    // de AdicionarAsync — VendedorIdSeAtivo foi removido na Story 4.2
    public async Task AtualizarAsync(Anuncio anuncio) =>
        await _dbContext.SaveChangesAsync();

    public async Task ExcluirAsync(Anuncio anuncio)
    {
        // Foto é removida automaticamente via OnDelete(Cascade) (AnuncioConfiguration, Story 2.1) —
        // nenhuma tradução de DbUpdateException necessária pra violação de índice único, excluir nunca
        // viola VendedorIdSeAtivo (a linha simplesmente deixa de existir)
        _dbContext.Anuncios.Remove(anuncio);

        try
        {
            await _dbContext.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // duas exclusões concorrentes do mesmo Anúncio (duplo clique, duas abas) — a segunda
            // encontra 0 linhas afetadas pelo DELETE porque a primeira já tinha excluído a linha.
            // O resultado desejado (o Anúncio não existir mais) já foi alcançado, então isso não é
            // um erro do ponto de vista do chamador — idempotente, sem propagar exceção
        }
    }

    // .AsNoTracking() — leitura pura pra exibição, nada aqui será salvo de volta no mesmo request
    // (mesmo padrão de VendedorRepository.ObterPorEmailAsync, Story 1.3)
    public async Task<IReadOnlyList<Anuncio>> ListarPorVendedorAsync(Guid vendedorId) =>
        await _dbContext.Anuncios
            .AsNoTracking()
            .Include(a => a.Fotos)
            .Where(a => a.VendedorId == vendedorId)
            .OrderByDescending(a => a.CriadoEm)
            .ToListAsync();

    // primeira query pública do projeto (Story 3.1) — Status == Ativo é imposto aqui sempre,
    // caminho novo e distinto de ListarPorVendedorAsync (owner-only, todos os status).
    // Marca/Modelo/Versao usam Contains (substring, case-insensitive via ToLower — "encontre
    // rapidamente" tolera substring); Ano exato; Estado/Cidade exato (campos curtos, tipo enum
    // informal, substring não faz sentido); PrecoMin/PrecoMax como faixa. Ordenação parametrizável
    // via filtro.Ordenacao desde a Story 3.2 (Relevancia continua sendo CriadoEm decrescente,
    // mesmo critério "recência" já usado desde a Story 2.5/3.1)
    public async Task<IReadOnlyList<Anuncio>> BuscarAsync(FiltroBusca filtro)
    {
        var query = _dbContext.Anuncios
            .AsNoTracking()
            .Include(a => a.Fotos)
            .Where(a => a.Status == StatusAnuncio.Ativo);

        if (!string.IsNullOrWhiteSpace(filtro.Marca))
            query = query.Where(a => a.Marca != null && a.Marca.ToLower().Contains(filtro.Marca.ToLower()));

        if (!string.IsNullOrWhiteSpace(filtro.Modelo))
            query = query.Where(a => a.Modelo != null && a.Modelo.ToLower().Contains(filtro.Modelo.ToLower()));

        if (!string.IsNullOrWhiteSpace(filtro.Versao))
            query = query.Where(a => a.Versao != null && a.Versao.ToLower().Contains(filtro.Versao.ToLower()));

        if (filtro.Ano is not null)
            query = query.Where(a => a.Ano == filtro.Ano);

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
            query = query.Where(a => a.Estado != null && a.Estado.ToLower() == filtro.Estado.ToLower());

        if (!string.IsNullOrWhiteSpace(filtro.Cidade))
            query = query.Where(a => a.Cidade != null && a.Cidade.ToLower() == filtro.Cidade.ToLower());

        if (filtro.PrecoMin is not null)
            query = query.Where(a => a.Preco != null && a.Preco >= filtro.PrecoMin);

        if (filtro.PrecoMax is not null)
            query = query.Where(a => a.Preco != null && a.Preco <= filtro.PrecoMax);

        // Busca textual livre (Story 3.3) — o termo é dividido em tokens por espaço em branco
        // (ex.: "Civic 2019" → ["Civic", "2019"]); cada token precisa combinar com pelo menos um
        // dos 4 campos que a AC nomeia (Marca/Modelo/Versao/Descricao), e os tokens entre si se
        // acumulam com E (um Where por token, cada um OR entre os campos) — faz o próprio exemplo
        // da AC funcionar sem exigir que a string inteira apareça literal num único campo.
        // Split(null, ...) — achado no code review: Split(' ', ...) só separava por espaço literal,
        // ignorando tab/quebra de linha que um termo colado poderia conter. Cap de 10 tokens —
        // limita o tamanho da query gerada contra um endpoint público sem autenticação
        if (!string.IsNullOrWhiteSpace(filtro.TermoLivre))
        {
            var tokens = filtro.TermoLivre
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Take(MaxTokensTermoLivre);
            foreach (var token in tokens)
            {
                var tokenLower = token.ToLower();
                query = query.Where(a =>
                    (a.Marca != null && a.Marca.ToLower().Contains(tokenLower)) ||
                    (a.Modelo != null && a.Modelo.ToLower().Contains(tokenLower)) ||
                    (a.Versao != null && a.Versao.ToLower().Contains(tokenLower)) ||
                    (a.Descricao != null && a.Descricao.ToLower().Contains(tokenLower)));
            }
        }

        // ThenByDescending(CriadoEm) como desempate — achado no code review: sem uma chave
        // secundária, dois Anúncios com o mesmo Preco/Ano não têm ordem relativa definida no
        // MySQL, podendo reordenar entre requisições idênticas. ThenBy(Id) como desempate FINAL
        // em todas as ordenações (inclusive Relevancia) — achado no code review da Story 3.4:
        // CriadoEm sozinho não é garantidamente único (Anúncios criados no mesmo instante/tick),
        // e agora que existe paginação um empate real pode causar item duplicado ou pulado entre
        // páginas, não só reordenação entre requisições isoladas como antes. Id é sempre único
        // Anúncio Patrocinado (Story 4.1) — IRankingService (AD-5) decide se o patrocínio prioriza
        // esta ordenação; hoje só Relevancia, nunca as 4 ordenações explícitas de campo (um Comprador
        // que pede "menor preço primeiro" não deveria ver o patrocínio sobrepor a ordenação escolhida)
        IQueryable<Anuncio> queryOrdenada = filtro.Ordenacao switch
        {
            OrdenacaoBusca.PrecoAsc => query.OrderBy(a => a.Preco).ThenByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
            OrdenacaoBusca.PrecoDesc => query.OrderByDescending(a => a.Preco).ThenByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
            OrdenacaoBusca.AnoAsc => query.OrderBy(a => a.Ano).ThenByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
            OrdenacaoBusca.AnoDesc => query.OrderByDescending(a => a.Ano).ThenByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
            _ when _rankingService.PriorizaPatrocinado(filtro.Ordenacao) =>
                query.OrderByDescending(a => a.Patrocinado).ThenByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
            _ => query.OrderByDescending(a => a.CriadoEm).ThenBy(a => a.Id),
        };

        // long em vez de int no cálculo do offset — achado no code review: um `pagina` muito
        // grande (ex. próximo de int.MaxValue) fazia (pagina - 1) * TamanhoPagina estourar o
        // range de int e virar negativo, quebrando o Skip() com um 500 não tratado num endpoint
        // público sem autenticação. Clamp num teto razoável em vez de deixar crescer sem limite
        var pagina = Math.Max(filtro.Pagina, 1);
        var offset = Math.Min((long)(pagina - 1) * TamanhoPagina, int.MaxValue);
        return await queryOrdenada.Skip((int)offset).Take(TamanhoPagina).ToListAsync();
    }

    // detalhe público (Story 3.5) — mesma regra de BuscarAsync (só Ativo é visível publicamente,
    // AD-10), distinto de ObterPorIdAsync (owner-only, qualquer status). .AsNoTracking() —
    // leitura pura pra exibição, mesmo padrão de BuscarAsync/ListarPorVendedorAsync
    public async Task<Anuncio?> ObterAtivoPorIdAsync(Guid id) =>
        await _dbContext.Anuncios
            .AsNoTracking()
            .Include(a => a.Fotos)
            .FirstOrDefaultAsync(a => a.Id == id && a.Status == StatusAnuncio.Ativo);

    // busca própria e rastreada (sem .AsNoTracking(), sem Include(Fotos) — não precisa das fotos
    // só pra incrementar um contador), distinta de ObterAtivoPorIdAsync (Story 4.5, ver Dev Notes
    // da story e IAnuncioRepository). Se o Anúncio não existir mais (corrida rara: excluído entre
    // a leitura do detalhe e este incremento), não lança nada — melhor esforço, mesmo espírito da
    // limpeza de mídia em GerenciarAnuncioService.ExcluirAsync (Story 2.4)
    public async Task IncrementarVisualizacaoAsync(Guid id)
    {
        var anuncio = await _dbContext.Anuncios.FirstOrDefaultAsync(a => a.Id == id);
        if (anuncio is null) return;

        anuncio.RegistrarVisualizacao();
        await _dbContext.SaveChangesAsync();
    }
}
