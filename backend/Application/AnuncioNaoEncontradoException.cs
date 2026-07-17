namespace Carango.Application;

public class AnuncioNaoEncontradoException : Exception
{
    public AnuncioNaoEncontradoException()
        : base("Anúncio não encontrado.")
    {
        // mensagem fixa — não interpola dado do usuário
    }
}
