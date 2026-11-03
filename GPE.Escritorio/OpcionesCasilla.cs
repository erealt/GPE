using GPE.Datos;

namespace GPE.Escritorio;

// Panel de una casilla (o fila o máquina) de la rutina. No toca la ronda: la pantalla lee lo elegido.
public class OpcionesCasilla : PanelEmergente
{
    // Lo elegido. EligioEstado: se pulsó un estado o "Vaciar" (Estado null = vaciar).
    public bool EligioEstado { get; private set; }
    public string? Estado { get; private set; }
    public bool SoloVacias { get; private set; }   // "Todo correcto": solo en las que están vacías
    public bool PideIncidencia { get; private set; }

    // incidencia: texto del botón ("Abrir incidencia…", "Ver incidencia 45") o null si no lleva
    public OpcionesCasilla(string titulo, int casillas, bool numerico, string? estadoActual, string? incidencia)
    {
        var pila = Rejilla();
        AnadirAncho(pila, Titulo(titulo));

        bool varias = casillas > 1;
        if (varias && !numerico)
        {
            // Lo habitual es que casi todo esté bien: se marca de golpe y se corrige lo que no
            var todo = BotonEstado(EstadoCasilla.Correcto, $"✓   Todo correcto ({casillas})", elegido: false);
            todo.Width = AnchoDoble;
            todo.Click += (s, e) => { SoloVacias = true; Elegir(EstadoCasilla.Correcto); };
            AnadirAncho(pila, todo);
            AnadirAncho(pila, Nota("Solo rellena las vacías: lo ya marcado no cambia. Para cambiarlo todo, elige un estado:"));
        }

        if (!numerico)
            foreach (string estado in EstadoCasilla.Todos)
            {
                var boton = BotonEstado(estado, $"{Simbolo(estado)}   {EstadoCasilla.Nombre(estado)}", estado == estadoActual);
                string elegido = estado;
                boton.Click += (s, e) => Elegir(elegido);
                pila.Controls.Add(boton);
            }

        // Para cerrar sin cambiar nada se pulsa fuera
        var vaciar = Tema.BotonSecundario(varias ? "Vaciar todas" : "Vaciar", AnchoDoble);
        vaciar.Height = 44;
        vaciar.Margin = new Padding(0, 4, 0, 0);
        vaciar.Enabled = varias || estadoActual != null || numerico;
        vaciar.Click += (s, e) => Elegir(null);
        AnadirAncho(pila, vaciar);

        if (incidencia != null)
        {
            var btnIncidencia = Tema.BotonSecundario(incidencia, AnchoDoble);
            btnIncidencia.Height = 44;
            btnIncidencia.Margin = new Padding(0, 10, 0, 0);
            btnIncidencia.ForeColor = Tema.Error;
            btnIncidencia.Click += (s, e) => { PideIncidencia = true; Close(); };
            AnadirAncho(pila, btnIncidencia);
        }

        Controls.Add(pila);
    }

    private void Elegir(string? estado)
    {
        EligioEstado = true;
        Estado = estado;
        Close();
    }

    // El estado que ya tiene la casilla, con un borde de su color y un punto delante
    private static Button BotonEstado(string estado, string texto, bool elegido)
    {
        var (fondo, colorTexto) = Tema.ColorEstadoCasilla(estado);
        return BotonGrande((elegido ? "● " : "") + texto, fondo, colorTexto, elegido);
    }

    // Lo mismo que se ve en la casilla
    public static string Simbolo(string estado) => estado switch
    {
        EstadoCasilla.Correcto => "✓",
        EstadoCasilla.NoCorrecto => "NO",
        EstadoCasilla.Mantenimiento => "M",
        _ => "N/A"
    };
}
