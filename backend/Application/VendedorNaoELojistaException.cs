namespace Carango.Application;

public class VendedorNaoELojistaException : Exception
{
    public VendedorNaoELojistaException()
        : base("Só Vendedores do tipo Lojista podem assinar um Plano Lojista.")
    {
        // mensagem fixa — não interpola dado do usuário
    }
}
