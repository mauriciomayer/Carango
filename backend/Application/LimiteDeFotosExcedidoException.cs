namespace Carango.Application;

public class LimiteDeFotosExcedidoException : Exception
{
    // achado no code review: mensagem genérica fixa não diferenciava "lote grande demais" (que
    // ValidarFotos no Controller já cobre) de "já tem N fotos, esse lote passaria do total" — só
    // interpola contagens (int), nunca texto vindo do usuário
    public LimiteDeFotosExcedidoException(int quantidadeExistente, int quantidadeNoLote)
        : base($"Este Anúncio já tem {quantidadeExistente} foto(s). Adicionar {quantidadeNoLote} passaria do máximo de 10 fotos por Anúncio.")
    {
    }
}
