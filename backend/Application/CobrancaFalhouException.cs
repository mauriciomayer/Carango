namespace Carango.Application;

public class CobrancaFalhouException : Exception
{
    public CobrancaFalhouException(string motivo) : base(motivo)
    {
    }
}
