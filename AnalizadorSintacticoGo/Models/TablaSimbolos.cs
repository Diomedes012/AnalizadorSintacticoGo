using System.Collections.Generic;
using AnalizadorSintacticoGo.Models;

namespace AnalizadorSintacticoGo.Services
{
    public class TablaSimbolos
    {
        // Pila de ámbitos: Cada elemento es un Diccionario (Nombre -> Simbolo)
        private readonly Stack<Dictionary<string, Simbolo>> _pilaAmbitos;

        public TablaSimbolos()
        {
            _pilaAmbitos = new Stack<Dictionary<string, Simbolo>>();
            // Ámbito global inicial
            _pilaAmbitos.Push(new Dictionary<string, Simbolo>());
        }

        public void EntrarAmbito()
        {
            _pilaAmbitos.Push(new Dictionary<string, Simbolo>());
        }

        public void SalirAmbito()
        {
            if (_pilaAmbitos.Count > 1)
            {
                _pilaAmbitos.Pop();
            }
        }

        public void Declarar(Simbolo simbolo, List<AnalisisError> errores)
        {
            var ambitoActual = _pilaAmbitos.Peek();

            if (ambitoActual.ContainsKey(simbolo.Nombre))
            {
                errores.Add(new AnalisisError
                {
                    Tipo = "Semántico",
                    Mensaje = $"La variable '{simbolo.Nombre}' ya ha sido declarada en este ámbito.",
                    Linea = simbolo.LineaDeclaracion,
                    CodigoError = "SEM001"
                });
            }
            else
            {
                ambitoActual[simbolo.Nombre] = simbolo;
            }
        }

        public Simbolo Obtener(string nombre)
        {
            // Buscamos desde el ámbito actual hacia abajo (hasta el global)
            foreach (var ambito in _pilaAmbitos)
            {
                if (ambito.ContainsKey(nombre))
                {
                    return ambito[nombre];
                }
            }
            return null; // No existe
        }
    }
}