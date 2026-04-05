using System;
using System.Collections.Generic;
using AnalizadorSintacticoGo.Models;

namespace AnalizadorSintacticoGo.Services;

public class Parser
{
    private readonly List<Token> _tokens;
    private int _current = 0;

    public List<AnalisisError> Errores { get; private set; } = new List<AnalisisError>();
    private readonly TablaSimbolos _tablaSimbolos = new TablaSimbolos();

    public Parser(List<Token> tokens)
    {
        _tokens = tokens;
    }

    // --- Método Principal ---
    public List<Instruccion> Parse()
    {
        List<Instruccion> programa = new List<Instruccion>();

        if (Match(TokenType.KEYWORD, "package"))
        {
            try { Consume(TokenType.IDENTIFIER, "Se esperaba nombre de paquete."); }
            catch (ParseException) { Synchronize(); }
        }
        else { Error("Falta 'package'.", "SYN001"); }

        while (!IsAtEnd())
        {
            try
            {
                var instruccion = ParseTopLevelDeclaration();
                if (instruccion != null)
                {
                    programa.Add(instruccion);
                }
            }
            catch (ParseException) 
            { 
                Synchronize(); 
            }
            catch (Exception ex)
            {
                Errores.Add(new AnalisisError
                {
                    Mensaje = $"Error crítico del sistema: {ex.Message}",
                    Tipo = "Sintáctico",
                    CodigoError = "CRITICAL"
                });
                Synchronize();
            }
        }

        return programa;
    }

    // --- Reglas Gramaticales ---

    private Instruccion ParseTopLevelDeclaration()
    {
        if (Match(TokenType.KEYWORD, "func"))
        {
            return ParseFunction();
        }

        if (Match(TokenType.KEYWORD, "var"))
        {
            return ParseVarDeclaration();
        }

        if (Match(TokenType.KEYWORD, "import"))
        {
            Consume(TokenType.STRING, "Se esperaba una cadena de texto para el import.");
            return null;
        }

        Error($"Declaración no válida en el nivel superior: '{Peek().Valor}'", "SYN002");
        Synchronize();
        return null;
    }

    private Instruccion ParseFunction()
    {
        Token nombreToken = Consume(TokenType.IDENTIFIER, "Se esperaba un nombre para la función.");
        Consume(TokenType.DELIMITER, "(", "Se esperaba '(' después del nombre de la función.");


        Consume(TokenType.DELIMITER, ")", "Se esperaba ')' después de los parámetros.");

        if (Check(TokenType.IDENTIFIER) || Check(TokenType.KEYWORD))
        {
            Advance();
        }

        if (Check(TokenType.DELIMITER, "{"))
        {
            InstruccionBloque cuerpo = ParseBlock(); 

            return new InstruccionFuncion(nombreToken.Valor, cuerpo);
        }
        else
        {
            Error("Se esperaba '{' para iniciar el cuerpo de la función.", "SYN003");
            return null;
        }
    }

    private Instruccion ParseVarDeclaration()
    {
        Token nombreToken = Consume(TokenType.IDENTIFIER, "Se esperaba un nombre de variable.");
        string tipoDato = "inferido";

        var nuevoSimbolo = new Simbolo { Nombre = nombreToken.Valor, Categoria = "var", LineaDeclaracion = nombreToken.Linea };
        _tablaSimbolos.Declarar(nuevoSimbolo, Errores);

        if (Check(TokenType.IDENTIFIER) || Check(TokenType.KEYWORD))
        {
            tipoDato = Advance().Valor;
        }

        Expresion valorInicial = null;
        if (Match(TokenType.OPERATOR, "="))
        {
            valorInicial = ParseExpression();
        }

        return new InstruccionDeclaracion(nombreToken.Valor, tipoDato, valorInicial);
    }

    private InstruccionBloque ParseBlock()
    {
        Consume(TokenType.DELIMITER, "{", "Se esperaba '{'.");
        _tablaSimbolos.EntrarAmbito();

        var bloque = new InstruccionBloque();

        while (!Check(TokenType.DELIMITER, "}") && !IsAtEnd())
        {
            var instruccion = ParseStatement();
            if (instruccion != null)
            {
                bloque.Instrucciones.Add(instruccion);
            }
        }

        _tablaSimbolos.SalirAmbito();
        Consume(TokenType.DELIMITER, "}", "Se esperaba '}' al final del bloque.");

        return bloque;
    }

    private Instruccion ParseStatement()
    {
        if (Match(TokenType.KEYWORD, "if"))
        {
            Expresion condicion = ParseExpression();
            InstruccionBloque ramaVerdadera = ParseBlock();
            InstruccionBloque ramaFalsa = null;

            if (Match(TokenType.KEYWORD, "else"))
            {
                ramaFalsa = ParseBlock();
            }

            return new InstruccionIf(condicion, ramaVerdadera, ramaFalsa);
        }

        if (Match(TokenType.KEYWORD, "for"))
        {
            Expresion condicion = ParseExpression();
            InstruccionBloque cuerpo = ParseBlock();

            return new InstruccionFor(condicion, cuerpo);
        }

        if (Match(TokenType.KEYWORD, "var"))
        {
            return ParseVarDeclaration();
        }

        return ParseExpressionStatement();
    }

    private Instruccion ParseExpressionStatement()
    {
        Token posibleVar = Peek();
        Expresion ladoIzquierdo = ParseExpression();

        if (ladoIzquierdo is ExpresionIdentificador id && id.Nombre == "print")
        {
            Expresion aImprimir = ParseExpression();
            return new InstruccionPrint(aImprimir);
        }

        if (ladoIzquierdo is ExpresionIndice exprIndice && Match(TokenType.OPERATOR, "="))
        {
            Expresion nuevoValor = ParseExpression();
            return new InstruccionAsignacionIndice(exprIndice.NombreArreglo, exprIndice.Indice, nuevoValor);
        }

        if (Match(TokenType.OPERATOR, ":="))
        {
            if (posibleVar.Tipo == TokenType.IDENTIFIER)
            {
                var nuevoSimbolo = new Simbolo { Nombre = posibleVar.Valor, Categoria = "var" };
                _tablaSimbolos.Declarar(nuevoSimbolo, Errores);
            }
            Expresion valorInicial = ParseExpression();
            return new InstruccionDeclaracion(posibleVar.Valor, "inferido", valorInicial);
        }

        else if (Match(TokenType.OPERATOR, "="))
        {
            if (ladoIzquierdo is ExpresionIdentificador)
            {
                Expresion nuevoValor = ParseExpression();
                return new InstruccionAsignacion(posibleVar.Valor, nuevoValor);
            }
            else
            {
                throw Error("Asignación inválida. No se puede asignar a esta expresión.", "SYN_ASSIGN");
            }
        }

        return null;
    }

    private Expresion ParseExpression()
    {
        return ParseLogicoOr();
    }

    private Expresion ParseLogicoOr()
    {
        Expresion izq = ParseLogicoAnd();
        while (Match(TokenType.OPERATOR, "||"))
        {
            Token operador = Previous();
            Expresion der = ParseLogicoAnd();
            izq = new ExpresionBinaria(izq, operador.Valor, der);
        }
        return izq;
    }

    private Expresion ParseLogicoAnd()
    {
        Expresion izq = ParseIgualdad();
        while (Match(TokenType.OPERATOR, "&&"))
        {
            Token operador = Previous();
            Expresion der = ParseIgualdad();
            izq = new ExpresionBinaria(izq, operador.Valor, der);
        }
        return izq;
    }

    private Expresion ParseIgualdad()
    {
        Expresion izq = ParseComparacion();
        while (Match(TokenType.OPERATOR, "==") || Match(TokenType.OPERATOR, "!="))
        {
            Token operador = Previous();
            Expresion der = ParseComparacion();
            izq = new ExpresionBinaria(izq, operador.Valor, der);
        }
        return izq;
    }

    private Expresion ParseComparacion()
    {
        Expresion izq = ParseTermino();
        while (Match(TokenType.OPERATOR, ">") || Match(TokenType.OPERATOR, ">=") ||
               Match(TokenType.OPERATOR, "<") || Match(TokenType.OPERATOR, "<="))
        {
            Token operador = Previous();
            Expresion der = ParseTermino();
            izq = new ExpresionBinaria(izq, operador.Valor, der);
        }
        return izq;
    }

    private Expresion ParseTermino()
    {
        Expresion izq = ParseFactor();
        while (Match(TokenType.OPERATOR, "+") || Match(TokenType.OPERATOR, "-"))
        {
            Token operador = Previous();
            Expresion der = ParseFactor();
            izq = new ExpresionBinaria(izq, operador.Valor, der);
        }
        return izq;
    }

    private Expresion ParseFactor()
    {
        Expresion izq = ParseUnario();
        while (Match(TokenType.OPERATOR, "*") || Match(TokenType.OPERATOR, "/"))
        {
            Token operador = Previous();
            Expresion der = ParseUnario();
            izq = new ExpresionBinaria(izq, operador.Valor, der);
        }
        return izq;
    }

    private Expresion ParseUnario()
    {
        if (Match(TokenType.OPERATOR, "!"))
        {
            Token operador = Previous();
            Expresion der = ParseUnario();
            return new ExpresionUnaria(operador.Valor, der);
        }
        return ParsePrimario();
    }

    private Expresion ParsePrimario()
    {
        if (Match(TokenType.KEYWORD, "true")) return new ExpresionBooleana(true);
        if (Match(TokenType.KEYWORD, "false")) return new ExpresionBooleana(false);

        if (Match(TokenType.DELIMITER, "("))
        {
            Expresion expr = ParseExpression();
            Consume(TokenType.DELIMITER, ")", "Falta cerrar el paréntesis ')'.");
            return expr;
        }

        if (Check(TokenType.IDENTIFIER))
        {
            Token tokenUso = Peek();
            if (tokenUso.Valor != "print")
            {
                Simbolo sim = _tablaSimbolos.Obtener(tokenUso.Valor);
                if (sim == null)
                    Errores.Add(new AnalisisError { Tipo = "Semántico", Linea = tokenUso.Linea, Mensaje = $"Variable '{tokenUso.Valor}' no declarada.", CodigoError = "SEM002" });
            }
            Advance();
            if (Match(TokenType.DELIMITER, "["))
            {
                Expresion indice = ParseExpression();
                Consume(TokenType.DELIMITER, "]", "Falta cerrar el corchete ']' del índice.");

                return new ExpresionIndice(tokenUso.Valor, indice);
            }

            return new ExpresionIdentificador(tokenUso.Valor);
        }

        if (Check(TokenType.NUMBER))
        {
            Token tokenNum = Advance();
            double valor = double.Parse(tokenNum.Valor, System.Globalization.CultureInfo.InvariantCulture);
            return new ExpresionNumero(valor);
        }

        if (Check(TokenType.STRING))
        {
            Token tokenStr = Advance();
            return new ExpresionCadena(tokenStr.Valor);
        }

        if (Check(TokenType.IDENTIFIER))
        {
            Token tokenUso = Peek();
            if (tokenUso.Valor != "print")
            {
                Simbolo sim = _tablaSimbolos.Obtener(tokenUso.Valor);
                if (sim == null)
                    Errores.Add(new AnalisisError { Tipo = "Semántico", Linea = tokenUso.Linea, Mensaje = $"Variable '{tokenUso.Valor}' no declarada.", CodigoError = "SEM002" });
            }
            Advance();

            if (Match(TokenType.DELIMITER, "["))
            {
                Expresion indice = ParseExpression();
                Consume(TokenType.DELIMITER, "]", "Falta cerrar el corchete ']' del índice.");
                return new ExpresionIndice(tokenUso.Valor, indice);
            }

            return new ExpresionIdentificador(tokenUso.Valor);
        }

        if (Match(TokenType.DELIMITER, "["))
        {
            List<Expresion> elementos = new List<Expresion>();

            if (!Check(TokenType.DELIMITER, "]"))
            {
                do
                {
                    elementos.Add(ParseExpression());
                } while (Match(TokenType.DELIMITER, ","));
            }

            Consume(TokenType.DELIMITER, "]", "Se esperaba ']' al final de la lista.");
            return new ExpresionArreglo(elementos);
        }

        if (Check(TokenType.DELIMITER, "}") || Check(TokenType.DELIMITER, ";") || Check(TokenType.DELIMITER, ")"))
            return null;

        throw Error("Se esperaba una expresión (variable, número, cadena o paréntesis).", "SYN_EXP");
    }

    // --- Helpers de Navegación y Verificación ---

    private bool Match(TokenType type, string value = null)
    {
        if (Check(type, value))
        {
            Advance();
            return true;
        }
        return false;
    }

    private bool Check(TokenType type, string value = null)
    {
        if (IsAtEnd()) return false;
        var t = Peek();
        if (t.Tipo != type) return false;
        if (value != null && t.Valor != value) return false;
        return true;
    }

    private Token Consume(TokenType type, string errorMessage)
    {
        if (Check(type)) return Advance();
        throw Error(errorMessage, "SYN_GEN");
    }

    private Token Consume(TokenType type, string value, string errorMessage)
    {
        if (Check(type, value)) return Advance();
        throw Error(errorMessage, "SYN_GEN");
    }

    private Token Advance()
    {
        if (!IsAtEnd()) _current++;
        return Previous();
    }

    private bool IsAtEnd()
    {
        return Peek().Tipo == TokenType.EOF;
    }

    private Token Peek()
    {
        return _tokens[_current];
    }

    private Token Previous()
    {
        return _tokens[_current - 1];
    }

    // --- Gestión de Errores ---

    private ParseException Error(string message, string code)
    {
        var token = Peek();
        var error = new AnalisisError
        {
            Linea = token.Linea,
            Columna = token.Columna,
            Tipo = "Sintáctico",
            Mensaje = $"{message} (Token encontrado: '{token.Valor}')",
            CodigoError = code
        };
        Errores.Add(error);
        return new ParseException();
    }

    // Sincronización
    private void Synchronize()
    {
        Advance();

        while (!IsAtEnd())
        {
            if (Previous().Tipo == TokenType.DELIMITER && Previous().Valor == ";") return;

            switch (Peek().Valor)
            {
                case "func":
                case "var":
                case "for":
                case "if":
                case "return":
                case "package":
                    return;
            }

            Advance();
        }
    }
}

public class ParseException : Exception { }