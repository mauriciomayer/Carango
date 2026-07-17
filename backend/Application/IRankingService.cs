namespace Carango.Application;

// Definida uma única vez (AD-5/AD-11) — único lugar que decide "quando o patrocínio deveria
// influenciar a ordenação de uma lista de Anúncios". AnuncioRepository consulta este serviço em
// vez de decidir sozinho, então qualquer endpoint futuro que produza uma lista ordenada de
// Anúncios reaproveita a mesma regra, sem duas implementações divergentes (AD-5)
public interface IRankingService
{
    bool PriorizaPatrocinado(OrdenacaoBusca ordenacao);
}
