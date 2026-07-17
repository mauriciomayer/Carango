namespace Carango.Application;

public class VeiculoReferenciaIndisponivelException : Exception
{
    public VeiculoReferenciaIndisponivelException()
        : base("Não foi possível carregar os dados da Fipe. Tente novamente.")
    {
        // mensagem fixa — não interpola dado do usuário
    }
}
