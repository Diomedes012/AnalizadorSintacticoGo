using System;
using System.Collections.Generic;
using AnalizadorSintacticoGo.Models;

namespace AnalizadorSintacticoGo.Services;

public class Parser
{
    private readonly List<Token> _tokens;
    private int _current = 0;

    // Lista de errores sintácticos
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
        // Si es una función, parseamos y devolvemos el nodo función
        if (Match(TokenType.KEYWORD, "func"))
        {
            return ParseFunction();
        }

        // Si es una variable global
        if (Match(TokenType.KEYWORD, "var"))
        {
            return ParseVarDeclaration();
        }

        // Si es un import (por ahora lo consumimos y devolvemos null para ignorarlo en la ejecución)
        if (Match(TokenType.KEYWORD, "import"))
        {
            Consume(TokenType.STRING, "Se esperaba una cadena de texto para el import.");
            return null;
        }

        // Si encontramos basura en el nivel superior
        Error($"Declaración no válida en el nivel superior: '{Peek().Valor}'", "SYN002");
        Synchronize(); // Intentar recuperarse
        return null;
    }

    private Instruccion ParseFunction()
    {
        // Estructura: func nombre ( ) { ... }
        Token nombreToken = Consume(TokenType.IDENTIFIER, "Se esperaba un nombre para la función.");
        Consume(TokenType.DELIMITER, "(", "Se esperaba '(' después del nombre de la función.");

        // Parámetros irían aquí...

        Consume(TokenType.DELIMITER, ")", "Se esperaba ')' después de los parámetros.");

        // Verificamos si hay tipo de retorno (ej: int, string)
        if (Check(TokenType.IDENTIFIER) || Check(TokenType.KEYWORD))
        {
            Advance();
        }

        // Cuerpo de la función
        if (Check(TokenType.DELIMITER, "{"))
        {
            InstruccionBloque cuerpo = ParseBlock(); // Obtenemos el bloque completo

            // Retornamos el nodo que representa la función entera
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
            tipoDato = Advance().Valor; // Guardamos el tipo (int, string, etc)
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

        var bloque = new InstruccionBloque(); // Creamos el nodo del bloque

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
        // 1. Sentencia IF
        if (Match(TokenType.KEYWORD, "if"))
        {
            Expresion condicion = ParseExpression();
            InstruccionBloque ramaVerdadera = ParseBlock();
            InstruccionBloque ramaFalsa = null;

            // Verificamos si existe un Else después del bloque true
            if (Match(TokenType.KEYWORD, "else"))
            {
                ramaFalsa = ParseBlock();
            }

            return new InstruccionIf(condicion, ramaVerdadera, ramaFalsa);
        }

        // 2. Declaración explícita (var)
        if (Match(TokenType.KEYWORD, "var"))
        {
            return ParseVarDeclaration();
        }

        // 3. Expresiones o Asignaciones
        return ParseExpressionStatement();
    }

    private Instruccion ParseExpressionStatement()
    {
        Token posibleVar = Peek();
        Expresion ladoIzquierdo = ParseExpression();

        // 1. Caso especial: Nuestra función nativa 'print'
        if (ladoIzquierdo is ExpresionIdentificador id && id.Nombre == "print")
        {
            // Leemos la expresión matemática que está a la derecha del print
            Expresion aImprimir = ParseExpression();
            return new InstruccionPrint(aImprimir);
        }

        // 2. Declaración corta (:=)
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
        // 3. Asignación normal (=)
        else if (Match(TokenType.OPERATOR, "="))
        {
            Expresion nuevoValor = ParseExpression();
            return new InstruccionAsignacion(posibleVar.Valor, nuevoValor);
        }

        return null;
    }

    private Expresion ParseExpression()
    {
        Expresion expresionIzquierda = null;

        // 1. Caso: Paréntesis
        if (Match(TokenType.DELIMITER, "("))
        {
            expresionIzquierda = ParseExpression();
            Consume(TokenType.DELIMITER, ")", "Falta cerrar el paréntesis ')'.");
        }
        // 2. Caso: Variables
        else if (Check(TokenType.IDENTIFIER))
        {
            Token tokenUso = Peek();

            // Si la variable NO es "print", verificamos si existe en memoria
            if (tokenUso.Valor != "print")
            {
                Simbolo sim = _tablaSimbolos.Obtener(tokenUso.Valor);
                if (sim == null)
                {
                    Errores.Add(new AnalisisError
                    {
                        Tipo = "Semántico",
                        Linea = tokenUso.Linea,
                        Mensaje = $"Variable '{tokenUso.Valor}' no declarada.",
                        CodigoError = "SEM002"
                    });
                }
            }

            Advance();
            expresionIzquierda = new ExpresionIdentificador(tokenUso.Valor);
        }
        // 3. Caso: Números
        else if (Check(TokenType.NUMBER))
        {
            Token tokenNum = Advance();
            // Usamos InvariantCulture para evitar problemas con comas y puntos decimales
            double valor = double.Parse(tokenNum.Valor, System.Globalization.CultureInfo.InvariantCulture);
            expresionIzquierda = new ExpresionNumero(valor);
        }
        else if (Check(TokenType.STRING))
        {
            Token tokenStr = Advance();
            expresionIzquierda = new ExpresionCadena(tokenStr.Valor);
        }
        else
        {
            if (Check(TokenType.DELIMITER, "}") || Check(TokenType.DELIMITER, ";") || Check(TokenType.DELIMITER, ")")) return null;
            throw Error("Se esperaba una expresión (variable, número, cadena o paréntesis).", "SYN_EXP");
        }

        // 4. Operaciones Binarias (Armando el árbol)
        while (Match(TokenType.OPERATOR))
        {
            Token operador = Previous(); // Guardamos el operador (+, -, <, etc)
            Expresion expresionDerecha = ParseExpression(); // Leemos lo que hay a la derecha

            // El nuevo nodo izquierdo envuelve todo lo que hemos leído hasta ahora
            expresionIzquierda = new ExpresionBinaria(expresionIzquierda, operador.Valor, expresionDerecha);
        }

        return expresionIzquierda;
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
        throw Error(errorMessage, "SYN_GEN"); // Lanza excepción para salir del flujo actual
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

    // Sincronización: Recuperarse después de un error para no reportar cascada de basura
    private void Synchronize()
    {
        Advance();

        while (!IsAtEnd())
        {
            // Si encontramos un punto y coma, asumimos que acabó la sentencia
            if (Previous().Tipo == TokenType.DELIMITER && Previous().Valor == ";") return;

            // Si encontramos el inicio de una nueva declaración importante
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

// Excepción interna para control de flujo (no exponer fuera del parser)
public class ParseException : Exception { }