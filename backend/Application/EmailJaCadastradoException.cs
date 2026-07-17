namespace Carango.Application;

public class EmailJaCadastradoException : Exception
{
    public EmailJaCadastradoException()
        : base("E-mail já cadastrado.")
    {
        // mensagem fixa, sem interpolar o e-mail — evita vazar dado pessoal em log/telemetria de exceção (LGPD)
    }
}
