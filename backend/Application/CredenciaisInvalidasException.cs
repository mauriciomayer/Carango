namespace Carango.Application;

public class CredenciaisInvalidasException : Exception
{
    public CredenciaisInvalidasException()
        : base("E-mail ou senha inválidos.")
    {
        // mensagem fixa e idêntica para e-mail inexistente e senha incorreta — não revela qual dado errou (AC #2, FR-1)
    }
}
