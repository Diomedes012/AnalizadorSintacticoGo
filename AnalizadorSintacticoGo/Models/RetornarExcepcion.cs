namespace AnalizadorSintacticoGo.Models;

public class RetornarExcepcion : Exception
{
    public object Valor { get; }

    public RetornarExcepcion(object valor)
    {
        Valor = valor;
    }
}
