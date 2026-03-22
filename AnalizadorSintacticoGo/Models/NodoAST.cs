using System.Collections.Generic;

namespace AnalizadorSintacticoGo.Models;

// 1. CLASES BASE
public interface INodoAST { }

public abstract class Expresion : INodoAST { }

public abstract class Instruccion : INodoAST { }


// 2. EXPRESIONES

public class ExpresionNumero : Expresion
{
    public double Valor { get; set; }
    public ExpresionNumero(double valor) => Valor = valor;
}

public class ExpresionCadena : Expresion
{
    public string Valor { get; set; }
    public ExpresionCadena(string valor) => Valor = valor;
}

public class ExpresionIdentificador : Expresion
{
    public string Nombre { get; set; }
    public ExpresionIdentificador(string nombre) => Nombre = nombre;
}

public class ExpresionBinaria : Expresion
{
    public Expresion Izquierda { get; set; }
    public string Operador { get; set; }
    public Expresion Derecha { get; set; }

    public ExpresionBinaria(Expresion izq, string op, Expresion der)
    {
        Izquierda = izq;
        Operador = op;
        Derecha = der;
    }
}


// 3. INSTRUCCIONES

public class InstruccionDeclaracion : Instruccion
{
    public string NombreVariable { get; set; }
    public string TipoDato { get; set; }
    public Expresion ValorInicial { get; set; }

    public InstruccionDeclaracion(string nombre, string tipoDato, Expresion valorInicial)
    {
        NombreVariable = nombre;
        TipoDato = tipoDato;
        ValorInicial = valorInicial;
    }
}

public class InstruccionAsignacion : Instruccion
{
    public string NombreVariable { get; set; }
    public Expresion NuevoValor { get; set; }

    public InstruccionAsignacion(string nombre, Expresion valor)
    {
        NombreVariable = nombre;
        NuevoValor = valor;
    }
}

public class InstruccionBloque : Instruccion
{
    public List<Instruccion> Instrucciones { get; set; } = new List<Instruccion>();
}

// Para manejar el "if (condicion) { bloque }"
public class InstruccionIf : Instruccion
{
    public Expresion Condicion { get; set; }
    public InstruccionBloque RamaVerdadera { get; set; }
    public InstruccionBloque RamaFalsa { get; set; }

    public InstruccionIf(Expresion condicion, InstruccionBloque ramaVerdadera, InstruccionBloque ramaFalsa = null)
    {
        Condicion = condicion;
        RamaVerdadera = ramaVerdadera;
        RamaFalsa = ramaFalsa;
    }
}

// Función nativa para imprimir resultados en la pantalla
public class InstruccionPrint : Instruccion
{
    public Expresion ExpresionAImprimir { get; set; }
    public InstruccionPrint(Expresion expresion) => ExpresionAImprimir = expresion;
}

public class InstruccionFuncion : Instruccion
{
    public string Nombre { get; set; }
    public InstruccionBloque Cuerpo { get; set; }

    public InstruccionFuncion(string nombre, InstruccionBloque cuerpo)
    {
        Nombre = nombre;
        Cuerpo = cuerpo;
    }
}
