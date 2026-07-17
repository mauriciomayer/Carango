namespace Carango.Application;

public class LimiteDeAnunciosAtivosExcedidoException : Exception
{
    public LimiteDeAnunciosAtivosExcedidoException()
        : base("Você já tem um Anúncio ativo. Pause ou marque o anterior como vendido antes de publicar outro, ou salve este como rascunho.")
    {
        // mensagem fixa — não interpola dado do usuário
    }
}
