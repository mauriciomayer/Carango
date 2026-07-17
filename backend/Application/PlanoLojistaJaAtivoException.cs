namespace Carango.Application;

public class PlanoLojistaJaAtivoException : Exception
{
    public PlanoLojistaJaAtivoException()
        : base("Você já tem um Plano Lojista ativo.")
    {
        // mensagem fixa — não interpola dado do usuário
    }
}
