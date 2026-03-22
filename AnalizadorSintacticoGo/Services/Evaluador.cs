using System;
using System.Collections.Generic;
using AnalizadorSintacticoGo.Models;

namespace AnalizadorSintacticoGo.Services
{
    public class Evaluador
    {
        // Memoria para guardar los valores reales
        private readonly TablaSimbolos _memoria = new TablaSimbolos();

        // Aquí guardaremos todo lo que el programa quiera imprimir en pantalla
        public List<string> Consola { get; private set; } = new List<string>();

        // Errores que ocurren mientras el programa corre
        public List<string> ErroresEjecucion { get; private set; } = new List<string>();

        public void Ejecutar(List<Instruccion> programa)
        {
            Consola.Clear();
            ErroresEjecucion.Clear();

            foreach (var instruccion in programa)
            {
                try
                {
                    EjecutarInstruccion(instruccion);
                }
                catch (Exception ex)
                {
                    ErroresEjecucion.Add($"Error en tiempo de ejecución: {ex.Message}");
                    break; // Un error fatal detiene el programa
                }
            }
        }

        // --- EVALUADOR DE INSTRUCCIONES (Acciones) ---
        private void EjecutarInstruccion(Instruccion inst)
        {
            // Usamos Pattern Matching de C# para saber qué tipo de nodo es
            switch (inst)
            {
                case InstruccionDeclaracion dec:
                    object valorInicial = null;
                    if (dec.ValorInicial != null)
                    {
                        valorInicial = EvaluarExpresion(dec.ValorInicial);
                    }

                    // Guardamos en memoria con su VALOR real
                    var sim = new Simbolo { Nombre = dec.NombreVariable, Valor = valorInicial };
                    _memoria.Declarar(sim, new List<AnalisisError>());
                    break;

                case InstruccionAsignacion asig:
                    object nuevoValor = EvaluarExpresion(asig.NuevoValor);
                    var variable = _memoria.Obtener(asig.NombreVariable);
                    if (variable != null)
                    {
                        variable.Valor = nuevoValor; // Actualizamos la memoria
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
                            // Si la condición es falsa y tenemos un Else, ejecutamos esto
                            EjecutarInstruccion(ifInst.RamaFalsa);
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
                    // Evaluamos lo que sea que esté dentro del print y lo mandamos a la consola
                    object valorImprimir = EvaluarExpresion(printInst.ExpresionAImprimir);
                    Consola.Add(valorImprimir?.ToString() ?? "nil");
                    break;

                case InstruccionFuncion func:
                    // Como Go ejecuta todo desde func main(), si vemos la función main, 
                    // ejecutamos su bloque de código inmediatamente.
                    if (func.Nombre == "main")
                    {
                        EjecutarInstruccion(func.Cuerpo);
                    }
                    break;
            }
        }

        // --- EVALUADOR DE EXPRESIONES (Matemáticas y Lógica) ---
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
                    // Recursividad: Evaluamos la izquierda y la derecha hasta llegar a números
                    object izq = EvaluarExpresion(bin.Izquierda);
                    object der = EvaluarExpresion(bin.Derecha);

                    return CalcularOperacion(izq, bin.Operador, der);

                case ExpresionCadena cad:
                    return cad.Valor;

                default:
                    throw new Exception("Expresión no soportada por el intérprete.");
            }
        }

        // --- EL CEREBRO MATEMÁTICO ---
        private object CalcularOperacion(object izq, string op, object der)
        {
            // Solo operamos si ambos lados son números (doubles)
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
    }
}