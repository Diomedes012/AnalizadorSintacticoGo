namespace AnalizadorSintacticoGo.Models;

public class Simbolo
{
    public string Nombre { get; set; }
    public string TipoDato { get; set; }
    public string Categoria { get; set; }
    public int LineaDeclaracion { get; set; }
    public object Valor { get; set; }
}