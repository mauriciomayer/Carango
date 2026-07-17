namespace Carango.Application;

public class AnuncioNaoPertenceAoVendedorException : Exception
{
    public AnuncioNaoPertenceAoVendedorException()
        : base("Este Anúncio não pertence a você.")
    {
        // mensagem fixa — não interpola dado do usuário
    }
}
