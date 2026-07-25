namespace Carango.Domain;

public class Anuncio
{
    // achado em teste manual do usuário: Ano não tinha NENHUMA validação de faixa aqui — só a
    // presença era checada (ano is null). Um Ano absurdo (ex.: 1897) passava por Publicar()/
    // AtualizarFicha() sem erro nenhum e ficava persistido num Anuncio Ativo. Mesmo piso (1990)
    // já usado no filtro de busca (BuscaPage.tsx, ANO_MINIMO) — duplicado deliberadamente aqui
    // porque só o Domain é a fonte de verdade de validação (AD-1); o frontend replica o mesmo
    // valor só como UX (min/max no input), nunca a garantia real
    private const int AnoMinimo = 1990;

    private readonly List<Foto> _fotos = [];

    public Guid Id { get; private set; }
    public Guid VendedorId { get; private set; }
    public string? Marca { get; private set; }
    public string? Modelo { get; private set; }
    public int? Ano { get; private set; }
    public string? Versao { get; private set; }
    public decimal? Preco { get; private set; }
    public string? Descricao { get; private set; }
    public string? Estado { get; private set; }
    public string? Cidade { get; private set; }
    public StatusAnuncio Status { get; private set; }
    public DateTime CriadoEm { get; private set; }
    public bool Patrocinado { get; private set; }
    public int Visualizacoes { get; private set; }
    public IReadOnlyList<Foto> Fotos => _fotos;

    private Anuncio()
    {
        // EF Core
    }

    private Anuncio(
        Guid vendedorId, string? marca, string? modelo, int? ano, string? versao,
        decimal? preco, string? descricao, string? estado, string? cidade)
    {
        if (vendedorId == Guid.Empty)
            throw new ArgumentException("VendedorId é obrigatório.", nameof(vendedorId));

        Id = Guid.NewGuid();
        VendedorId = vendedorId;
        Marca = marca;
        Modelo = modelo;
        Ano = ano;
        Versao = versao;
        Preco = preco;
        Descricao = descricao;
        Estado = estado;
        Cidade = cidade;
        Status = StatusAnuncio.Rascunho;
        // primeiro campo de data/hora do projeto — sempre UTC ("datas em ISO 8601 UTC", ARCHITECTURE-SPINE.md)
        CriadoEm = DateTime.UtcNow;
    }

    public static Anuncio CriarRascunho(
        Guid vendedorId, string? marca = null, string? modelo = null, int? ano = null, string? versao = null,
        decimal? preco = null, string? descricao = null, string? estado = null, string? cidade = null)
        => new(vendedorId, marca, modelo, ano, versao, preco, descricao, estado, cidade);

    public void Publicar()
    {
        if (Status != StatusAnuncio.Rascunho)
            throw new InvalidOperationException($"Só é possível publicar um Anúncio a partir do status Rascunho (status atual: {Status}).");

        ValidarCamposObrigatorios(Marca, Modelo, Ano, Versao, Preco, Descricao, Estado, Cidade);

        Status = StatusAnuncio.Ativo;
    }

    // achado no code review da Story 2.8: Ordem = _fotos.Count só era seguro enquanto _fotos só
    // crescia (nunca existiu remoção antes desta story). Com RemoverFoto agora podendo encolher a
    // lista, usar Count de novo colidiria com o Ordem de uma foto que já existia (ex.: 3 fotos
    // Ordem 0/1/2, remove a de Ordem 0, adiciona uma nova → Count=2 colidiria com a foto que já
    // tem Ordem=2). Usar o maior Ordem já usado + 1 garante um valor sempre inédito
    public void AdicionarFoto(string url)
    {
        var proximaOrdem = _fotos.Count == 0 ? 0 : _fotos.Max(f => f.Ordem) + 1;
        _fotos.Add(new Foto(url, proximaOrdem));
    }

    // retorna a Url removida, ou null se nenhuma Foto com esse Id existir neste Anuncio — quem
    // decide se isso é um erro (e qual exceção lançar) é a Application, mesmo padrão de
    // AnuncioNaoEncontradoException já ser lançada em GerenciarAnuncioService a partir de um
    // retorno null do repositório, não de dentro do Domain (Domain só usa exceções padrão do
    // framework, ex. InvalidOperationException em Pausar/Reativar/etc.)
    public string? RemoverFoto(Guid fotoId)
    {
        var foto = _fotos.FirstOrDefault(f => f.Id == fotoId);
        if (foto is null)
            return null;

        _fotos.Remove(foto);
        return foto.Url;
    }

    public void Pausar()
    {
        if (Status != StatusAnuncio.Ativo)
            throw new InvalidOperationException($"Só é possível pausar um Anúncio a partir do status Ativo (status atual: {Status}).");

        // zera o patrocínio como parte da própria transição — mesmo lugar único que já muta Status,
        // garante AD-10 (Patrocinado só pode ser true quando Status = Ativo) sem depender de uma
        // checagem cruzada solta em Application que uma story futura poderia esquecer de chamar
        RemoverDestaque();
        Status = StatusAnuncio.Pausado;
    }

    public void Reativar()
    {
        if (Status != StatusAnuncio.Pausado)
            throw new InvalidOperationException($"Só é possível reativar um Anúncio a partir do status Pausado (status atual: {Status}).");

        // um Anúncio pausado pode ter sido editado com campos em branco (AtualizarFicha só valida
        // quando Status == Ativo) — reativar precisa da mesma garantia de Publicar(): nenhum Anúncio
        // Ativo pode ficar com a Ficha incompleta. Achado no code review, sem isso "pausar → limpar
        // campos → reativar" produzia um Ativo com Marca/Preco/etc. nulos
        ValidarCamposObrigatorios(Marca, Modelo, Ano, Versao, Preco, Descricao, Estado, Cidade);

        // a checagem de cota de 1-Anúncio-ativo não vive aqui — Domain não tem acesso a outros
        // Anúncios do mesmo Vendedor; isso é responsabilidade da Application (GerenciarAnuncioService)
        Status = StatusAnuncio.Ativo;
    }

    public void MarcarComoVendido()
    {
        if (Status != StatusAnuncio.Ativo && Status != StatusAnuncio.Pausado)
            throw new InvalidOperationException($"Só é possível marcar como vendido um Anúncio a partir do status Ativo ou Pausado (status atual: {Status}).");

        RemoverDestaque();
        Status = StatusAnuncio.Vendido;
    }

    public void Destacar()
    {
        if (Status != StatusAnuncio.Ativo)
            throw new InvalidOperationException($"Só é possível destacar um Anúncio a partir do status Ativo (status atual: {Status}).");

        Patrocinado = true;
    }

    // idempotente — não lança se já for false, mesmo espírito de ExcluirAsync (Story 2.4):
    // o estado desejado ("sem destaque") já foi alcançado, não é um erro do ponto de vista do chamador
    public void RemoverDestaque()
    {
        Patrocinado = false;
    }

    public void RegistrarVisualizacao()
    {
        Visualizacoes++;
    }

    public void AtualizarFicha(
        string? marca, string? modelo, int? ano, string? versao,
        decimal? preco, string? descricao, string? estado, string? cidade)
    {
        // valida ANTES de aplicar qualquer campo — um Anúncio ativo nunca pode ficar com a Ficha
        // incompleta mesmo que a validação falhe (Rascunho continua com a mesma liberdade da criação)
        if (Status == StatusAnuncio.Ativo)
            ValidarCamposObrigatorios(marca, modelo, ano, versao, preco, descricao, estado, cidade);

        Marca = marca;
        Modelo = modelo;
        Ano = ano;
        Versao = versao;
        Preco = preco;
        Descricao = descricao;
        Estado = estado;
        Cidade = cidade;
    }

    private static void ValidarCamposObrigatorios(
        string? marca, string? modelo, int? ano, string? versao,
        decimal? preco, string? descricao, string? estado, string? cidade)
    {
        var camposFaltando = new List<string>();
        if (string.IsNullOrWhiteSpace(marca)) camposFaltando.Add(nameof(Marca));
        if (string.IsNullOrWhiteSpace(modelo)) camposFaltando.Add(nameof(Modelo));
        if (ano is null) camposFaltando.Add(nameof(Ano));
        if (string.IsNullOrWhiteSpace(versao)) camposFaltando.Add(nameof(Versao));
        if (preco is null) camposFaltando.Add(nameof(Preco));
        if (string.IsNullOrWhiteSpace(descricao)) camposFaltando.Add(nameof(Descricao));
        if (string.IsNullOrWhiteSpace(estado)) camposFaltando.Add(nameof(Estado));
        if (string.IsNullOrWhiteSpace(cidade)) camposFaltando.Add(nameof(Cidade));

        if (camposFaltando.Count > 0)
            throw new ArgumentException($"Campos obrigatórios ausentes para publicar: {string.Join(", ", camposFaltando)}.");

        // preço zero/negativo não é "campo ausente", mas é um valor sem sentido pra qualquer veículo —
        // checagem separada da lista acima porque a mensagem é sobre o valor, não sobre a presença
        if (preco <= 0)
            throw new ArgumentException("Preço deve ser maior que zero.", nameof(preco));

        // mesmo espírito da checagem de preço acima: Ano presente mas fora de uma faixa plausível
        // não é "campo ausente", é valor sem sentido. Teto é o ano corrente + 1 (não hardcoded) —
        // "ano modelo" de um 0km pode ser o ano seguinte ao da venda, mesma regra já usada no
        // filtro de busca
        var anoMaximo = DateTime.UtcNow.Year + 1;
        if (ano < AnoMinimo || ano > anoMaximo)
            throw new ArgumentException($"Ano deve estar entre {AnoMinimo} e {anoMaximo}.", nameof(ano));
    }
}
