namespace AnalizadorSintacticoGo.Models;

public enum TokenType
{
    KEYWORD,    // func, var, if, else...
    IDENTIFIER, // nombreVariable, miFuncion...
    NUMBER,     // 123, 4.5
    STRING,     // "Hola mundo"
    OPERATOR,   // +, -, *, /, =, :=, ==
    DELIMITER,  // (, ), {, }, [, ], ,, ;
    EOF,        // Fin del archivo
    UNKNOWN     // Error léxico
}

public class Token
{
    public TokenType Tipo { get; set; }
    public string Valor { get; set; }
    public int Linea { get; set; }
    public int Columna { get; set; }

    public override string ToString() => $"{Tipo}: '{Valor}' (Ln {Linea}, Col {Columna})";
}
