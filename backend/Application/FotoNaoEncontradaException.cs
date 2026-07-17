namespace Carango.Application;

public class FotoNaoEncontradaException : Exception
{
    public FotoNaoEncontradaException()
        : base("Foto não encontrada.")
    {
        // mensagem fixa — não interpola dado do usuário
    }
}
