namespace AnalizadorSintacticoGo.Models;

public class AnalisisError
{
    public int Linea { get; set; }
    public int Columna { get; set; }
    public string Tipo { get; set; }
    public string Mensaje { get; set; } 
    public string CodigoError { get; set; }
}
