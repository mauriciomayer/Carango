namespace Carango.Application;

public class PlanoLojistaNaoEncontradoException : Exception
{
    public PlanoLojistaNaoEncontradoException()
        : base("Você não tem um Plano Lojista.")
    {
        // mensagem fixa — não interpola dado do usuário
    }
}
