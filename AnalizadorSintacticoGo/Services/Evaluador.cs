using System;
using System.Collections.Generic;
using AnalizadorSintacticoGo.Models;

namespace AnalizadorSintacticoGo.Services;

public class Evaluador
{
    private readonly TablaSimbolos _memoria = new TablaSimbolos();

    public List<string> Consola { get; private set; } = new List<string>();

    public List<string> ErroresEjecucion { get; private set; } = new List<string>();

    // El diccionario para guardar todas las funciones creadas por el usuario
    private Dictionary<string, InstruccionFuncion> _funciones = new Dictionary<string, InstruccionFuncion>();

    public void Ejecutar(List<Instruccion> programa)
    {
        Consola.Clear();
        ErroresEjecucion.Clear();
        _funciones.Clear();

        try
        {
            foreach (var instruccion in programa)
            {
                if (instruccion is InstruccionFuncion func)
                {
                    _funciones[func.Nombre] = func;
                }
                else
                {
                    EjecutarInstruccion(instruccion);
                }
            }

            if (_funciones.ContainsKey("main"))
            {
                LlamarFuncion("main", new List<object>());
            }
            else
            {
                ErroresEjecucion.Add("Error: El programa debe contener una función 'main()'.");
            }
        }
        catch (Exception ex)
        {
            ErroresEjecucion.Add($"Error en tiempo de ejecución: {ex.Message}");
        }
    }

    private void EjecutarInstruccion(Instruccion inst)
    {
        switch (inst)
        {
            case InstruccionDeclaracion dec:
                object valorInicial = null;
                if (dec.ValorInicial != null)
                {
                    valorInicial = EvaluarExpresion(dec.ValorInicial);
                }

                var sim = new Simbolo { Nombre = dec.NombreVariable, Valor = valorInicial };
                _memoria.Declarar(sim, new List<AnalisisError>());
                break;

            case InstruccionAsignacion asig:
                object nuevoValor = EvaluarExpresion(asig.NuevoValor);
                var variable = _memoria.Obtener(asig.NombreVariable);
                if (variable != null)
                {
                    variable.Valor = nuevoValor;
                }
                break;

            case InstruccionIf ifInst:
                object condicion = EvaluarExpresion(ifInst.Condicion);
                if (condicion is bool b)
                {
                    if (b)
                    {
                        EjecutarInstruccion(ifInst.RamaVerdadera);
                    }
                    else if (ifInst.RamaFalsa != null)
                    {
                        EjecutarInstruccion(ifInst.RamaFalsa);
                    }
                }
                break;

            case InstruccionFor forInst:
                int limiteIteraciones = 10000;
                int iteracionActual = 0;

                while (true)
                {
                    object cond = EvaluarExpresion(forInst.Condicion);

                    if (cond is bool bFor && bFor)
                    {
                        EjecutarInstruccion(forInst.Cuerpo);

                        iteracionActual++;
                        if (iteracionActual >= limiteIteraciones)
                        {
                            throw new Exception("Bucle infinito detenido por seguridad (> 10,000 iteraciones).");
                        }
                    }
                    else
                    {
                        break;
                    }
                }
                break;

            case InstruccionAsignacionIndice asigIndice:
                var varArr = _memoria.Obtener(asigIndice.NombreArreglo);
                if (varArr == null) throw new Exception($"El arreglo '{asigIndice.NombreArreglo}' no existe.");

                if (varArr.Valor is List<object> laLista)
                {
                    object indexVal = EvaluarExpresion(asigIndice.Indice);
                    if (indexVal is double d)
                    {
                        int i = (int)d;
                        if (i >= 0 && i < laLista.Count)
                        {
                            laLista[i] = EvaluarExpresion(asigIndice.NuevoValor);
                        }
                        else throw new Exception($"Índice [{i}] fuera de los límites.");
                    }
                }
                break;

            case InstruccionBloque bloque:
                _memoria.EntrarAmbito();
                foreach (var subInst in bloque.Instrucciones)
                {
                    EjecutarInstruccion(subInst);
                }
                _memoria.SalirAmbito();
                break;

            case InstruccionPrint printInst:
                object valorImprimir = EvaluarExpresion(printInst.ExpresionAImprimir);

                if (valorImprimir is List<object> lista)
                {
                    Consola.Add("[" + string.Join(", ", lista) + "]");
                }
                else if (valorImprimir is bool bVal)
                {
                    Consola.Add(bVal ? "true" : "false");
                }
                else
                {
                    var texto = valorImprimir?.ToString() ?? "nil";
                    if (valorImprimir is string strVal)
                    {
                        texto = strVal;
                    }
                    Consola.Add(texto);
                }
                break;

            case InstruccionFuncion func:
                break;

            case InstruccionReturn retInst:
                object valor = null;
                if (retInst.Valor != null)
                {
                    valor = EvaluarExpresion(retInst.Valor);
                }
                throw new Models.RetornarExcepcion(valor);
        }
    }

    private object EvaluarExpresion(Expresion expr)
    {
        switch (expr)
        {
            case ExpresionNumero num:
                return num.Valor;

            case ExpresionIdentificador id:
                var variable = _memoria.Obtener(id.Nombre);
                if (variable == null) throw new Exception($"La variable '{id.Nombre}' no existe en memoria.");
                return variable.Valor;

            case ExpresionBinaria bin:
                object izq = EvaluarExpresion(bin.Izquierda);
                object der = EvaluarExpresion(bin.Derecha);

                return CalcularOperacion(izq, bin.Operador, der);

            case ExpresionCadena cad:
                return cad.Valor;

            case ExpresionBooleana boolExpr:
                return boolExpr.Valor;

            case ExpresionArreglo arr:
                List<object> listaEnMemoria = new List<object>();
                foreach (var elemento in arr.Elementos)
                {
                    listaEnMemoria.Add(EvaluarExpresion(elemento));
                }
                return listaEnMemoria;

            case ExpresionIndice ind:
                var variableArreglo = _memoria.Obtener(ind.NombreArreglo);
                if (variableArreglo == null) throw new Exception($"El arreglo '{ind.NombreArreglo}' no existe.");

                if (variableArreglo.Valor is List<object> miLista)
                {
                    object indiceValor = EvaluarExpresion(ind.Indice);

                    if (indiceValor is double dIndice)
                    {
                        int i = (int)dIndice;
                        if (i >= 0 && i < miLista.Count) return miLista[i];
                        throw new Exception($"Índice [{i}] fuera de los límites del arreglo.");
                    }
                    throw new Exception("El índice del arreglo debe ser un número entero.");
                }
                throw new Exception($"La variable '{ind.NombreArreglo}' no es un arreglo. Su tipo real en memoria es: {variableArreglo.Valor?.GetType().Name ?? "nulo"}");

            case ExpresionUnaria un:
                object derecha = EvaluarExpresion(un.Derecha);
                if (un.Operador == "!" && derecha is bool bVal)
                {
                    return !bVal;
                }
                throw new Exception($"No se puede aplicar '{un.Operador}' al valor {derecha}.");

            case ExpresionLlamada llamada:
                List<object> argumentosEvaluados = new List<object>();
                foreach (var arg in llamada.Argumentos)
                {
                    argumentosEvaluados.Add(EvaluarExpresion(arg));
                }
                return LlamarFuncion(llamada.NombreFuncion, argumentosEvaluados);

            default:
                throw new Exception("Expresión no soportada por el intérprete.");
        }
    }

    private object CalcularOperacion(object izq, string op, object der)
    {
        if (izq is bool b1 && der is bool b2)
        {
            switch (op)
            {
                case "&&": return b1 && b2;
                case "||": return b1 || b2;
                case "==": return b1 == b2;
                case "!=": return b1 != b2;
            }
        }
        if (izq is double d1 && der is double d2)
        {
            switch (op)
            {
                case "+": return d1 + d2;
                case "-": return d1 - d2;
                case "*": return d1 * d2;
                case "/":
                    if (d2 == 0) throw new Exception("División por cero.");
                    return d1 / d2;
                case "<": return d1 < d2;
                case ">": return d1 > d2;
                case "<=": return d1 <= d2;
                case ">=": return d1 >= d2;
                case "==": return d1 == d2;
                case "!=": return d1 != d2;
            }
        }
        if (izq is string || der is string)
        {
            if (op == "+") return izq.ToString() + der.ToString();
            throw new Exception("Solo se puede usar '+' con cadenas de texto.");
        }

        throw new Exception($"Operación inválida: No se puede aplicar '{op}' entre {izq} y {der}.");
    }

    private object LlamarFuncion(string nombre, List<object> argumentos)
    {
        if (!_funciones.ContainsKey(nombre))
            throw new Exception($"La función '{nombre}' no está definida.");

        var funcion = _funciones[nombre];

        if (argumentos.Count != funcion.Parametros.Count)
            throw new Exception($"La función '{nombre}' espera {funcion.Parametros.Count} argumentos, pero recibió {argumentos.Count}.");

        _memoria.EntrarAmbito();

        try
        {
            for (int i = 0; i < funcion.Parametros.Count; i++)
            {
                _memoria.Declarar(new Simbolo { Nombre = funcion.Parametros[i], Valor = argumentos[i] }, new List<AnalisisError>());
            }

            EjecutarInstruccion(funcion.Cuerpo);
        }
        catch (Models.RetornarExcepcion ret) 
        {
            return ret.Valor;
        }
        finally
        {
            _memoria.SalirAmbito();
        }

        return null;
    }
}