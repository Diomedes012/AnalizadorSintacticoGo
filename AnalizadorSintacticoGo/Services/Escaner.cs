using System.Text;
using AnalizadorSintacticoGo.Models;

namespace AnalizadorSintacticoGo.Services;

public class Escaner
{
    private readonly string _source;
    private int _position;
    private int _linea;
    private int _columna;

    public List<AnalisisError> Errores { get; private set; } = new List<AnalisisError>();

    private static readonly HashSet<string> Keywords = new HashSet<string>
    {
        "break", "case", "chan", "const", "continue", "default", "defer", "else", "fallthrough", "false",
        "for", "func", "go", "goto", "if", "import", "interface", "map", "package", "range",
        "return", "select", "struct", "switch",  "true", "type", "var"
    };

    public Escaner(string source)
    {
        _source = source;
        _position = 0;
        _linea = 1;
        _columna = 0;
    }

    public List<Token> ScanTokens()
    {
        var tokens = new List<Token>();

        while (_position < _source.Length)
        {
            char current = Peek();

            if (char.IsWhiteSpace(current))
            {
                if (current == '\n')
                {
                    _linea++;
                    _columna = 0;
                }
                Advance();
                continue;
            }

            if (current == '/' && PeekNext() == '/')
            {
                while (Peek() != '\n' && Peek() != '\0') Advance();
                continue;
            }

            if (char.IsLetter(current) || current == '_')
            {
                tokens.Add(ReadIdentifier());
                continue;
            }

            if (char.IsDigit(current))
            {
                tokens.Add(ReadNumber());
                continue;
            }

            if (current == '"')
            {
                var stringToken = ReadString();
                if (stringToken != null)
                {
                    tokens.Add(stringToken);
                }
                continue;
            }

            Token token = ReadSymbol();
            if (token != null)
            {
                tokens.Add(token);
                continue;
            }

            int errLine = _linea;
            int errCol = _columna + 1;

            Errores.Add(new AnalisisError
            {
                Tipo = "Léxico",
                Linea = errLine,
                Columna = errCol,
                Mensaje = $"Carácter inválido detectado: '{current}'",
                CodigoError = "LEX001"
            });

            Advance();
        }

        tokens.Add(new Token { Tipo = TokenType.EOF, Valor = "", Linea = _linea, Columna = _columna });
        return tokens;
    }

    private Token ReadIdentifier()
    {
        int startCol = _columna + 1;
        StringBuilder sb = new StringBuilder();

        while (char.IsLetterOrDigit(Peek()) || Peek() == '_')
        {
            sb.Append(Advance());
        }

        string text = sb.ToString();
        TokenType type = Keywords.Contains(text) ? TokenType.KEYWORD : TokenType.IDENTIFIER;

        return new Token { Tipo = type, Valor = text, Linea = _linea, Columna = startCol };
    }

    private Token ReadNumber()
    {
        int startCol = _columna + 1;
        StringBuilder sb = new StringBuilder();

        while (char.IsDigit(Peek()))
        {
            sb.Append(Advance());
        }
        if (Peek() == '.' && char.IsDigit(PeekNext()))
        {
            sb.Append(Advance());
            while (char.IsDigit(Peek())) sb.Append(Advance());
        }

        return new Token { Tipo = TokenType.NUMBER, Valor = sb.ToString(), Linea = _linea, Columna = startCol };
    }

    private Token ReadString()
    {
        int startPosition = _position;

        int startCol = _columna;
        int startLine = _linea;

        Advance();

        while (Peek() != '"' && !IsAtEnd())
        {
            if (Peek() == '\n')
            {
                Errores.Add(new AnalisisError
                {
                    Tipo = "Léxico",
                    Linea = _linea,
                    Mensaje = "Salto de línea en cadena de texto (¿olvidaste cerrar comillas?).",
                    CodigoError = "LEX002"
                });
                _linea++;
                _columna = 0;
                return null;
            }
            Advance();
        }

        if (IsAtEnd())
        {
            Errores.Add(new AnalisisError
            {
                Tipo = "Léxico",
                Linea = startLine,
                Mensaje = "Cadena de texto no cerrada al final del archivo.",
                CodigoError = "LEX003"
            });
            return null;
        }

        Advance();

        string valor = _source.Substring(startPosition, _position - startPosition);
        return new Token { Tipo = TokenType.STRING, Valor = valor, Linea = startLine, Columna = startCol };
    }

    private Token ReadSymbol()
    {
        int startCol = _columna + 1;
        char current = Peek();

        if (current == ':' && PeekNext() == '=')
        {
            Advance(); Advance();
            return new Token { Tipo = TokenType.OPERATOR, Valor = ":=", Linea = _linea, Columna = startCol };
        }
        if (current == '=' && PeekNext() == '=')
        {
            Advance(); Advance();
            return new Token { Tipo = TokenType.OPERATOR, Valor = "==", Linea = _linea, Columna = startCol };
        }
        if (current == '!' && PeekNext() == '=')
        {
            Advance(); Advance();
            return new Token { Tipo = TokenType.OPERATOR, Valor = "!=", Linea = _linea, Columna = startCol };
        }
        if (current == '<' && PeekNext() == '=')
        {
            Advance(); Advance();
            return new Token { Tipo = TokenType.OPERATOR, Valor = "<=", Linea = _linea, Columna = startCol };
        }
        if (current == '>' && PeekNext() == '=')
        {
            Advance(); Advance();
            return new Token { Tipo = TokenType.OPERATOR, Valor = ">=", Linea = _linea, Columna = startCol };
        }
        if (current == '&' && PeekNext() == '&')
        {
            Advance(); Advance();
            return new Token { Tipo = TokenType.OPERATOR, Valor = "&&", Linea = _linea, Columna = startCol };
        }
        if (current == '|' && PeekNext() == '|')
        {
            Advance(); Advance();
            return new Token { Tipo = TokenType.OPERATOR, Valor = "||", Linea = _linea, Columna = startCol };
        }

        string operators = "+-*/=<>!";
        string delimiters = "(){}[],.;";

        if (operators.Contains(current))
        {
            Advance();
            return new Token { Tipo = TokenType.OPERATOR, Valor = current.ToString(), Linea = _linea, Columna = startCol };
        }

        if (delimiters.Contains(current))
        {
            Advance();
            return new Token { Tipo = TokenType.DELIMITER, Valor = current.ToString(), Linea = _linea, Columna = startCol };
        }

        return null;
    }

    // --- Navegación del puntero ---

    private char Peek()
    {
        if (_position >= _source.Length) return '\0';
        return _source[_position];
    }

    private char PeekNext()
    {
        if (_position + 1 >= _source.Length) return '\0';
        return _source[_position + 1];
    }

    private char Advance()
    {
        _columna++;
        return _source[_position++];
    }

    private bool IsAtEnd()
    {
        return _position >= _source.Length;
    }
}