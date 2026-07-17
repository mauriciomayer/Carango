namespace Carango.Application;

// Sem I/O — pura lógica de decisão, por isso vive em Application (não em Infrastructure). A AC
// da Story 4.1 amarra o patrocínio à ordenação por Relevância especificamente: um Comprador que
// pede explicitamente "menor preço primeiro" não deveria ver o patrocínio sobrepor a ordenação
// que ele escolheu — só as outras 4 ordenações (Preco/Ano Asc/Desc) ficam de fora de propósito
public class RankingService : IRankingService
{
    public bool PriorizaPatrocinado(OrdenacaoBusca ordenacao) => ordenacao == OrdenacaoBusca.Relevancia;
}
