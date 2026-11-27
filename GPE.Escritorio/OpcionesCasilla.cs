using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Panel de una casilla (o fila o máquina) de la rutina. No toca la ronda: la pantalla lee lo elegido.
public class OpcionesCasilla : PanelEmergente
{
    // Lo elegido. EligioEstado: se pulsó un estado o "Vaciar" (Estado null = vaciar).
    public bool EligioEstado { get; private set; }
    public string? Estado { get; private set; }
    public bool SoloVacias { get; private set; }   // "Todo correcto": solo en las que están vacías
    public bool PideIncidencia { get; private set; }
    public bool CambioComentario { get; private set; }
    public string? Comentario { get; private set; }

    private readonly TextBox? txtComentario;
    private readonly string comentarioInicial = "";

    // incidencia: texto del botón ("Abrir incidencia…", "Ver incidencia 45") o null si no lleva.
    // comentario: el de la casilla. Solo con una casilla se puede ver y cambiar, como en el móvil.
    public OpcionesCasilla(string titulo, int casillas, bool numerico, string? estadoActual, string? incidencia,
        string? comentario = null)
    {
        var pila = Rejilla();
        AnadirAncho(pila, Titulo(titulo));

        if (casillas == 1)
        {
            // Desde el móvil llegan saltos "\n"; el cuadro de texto de Windows necesita "\r\n"
            comentarioInicial = comentario?.ReplaceLineEndings("\r\n") ?? "";
            txtComentario = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                MaxLength = RutinaServicio.LargoComentario,
                Size = new Size(AnchoDoble, 64),
                Margin = new Padding(0, 0, 0, 10),
                PlaceholderText = "Comentario (opcional)",
                Text = comentarioInicial,
                BackColor = comentario != null ? Tema.AcentoSuave : Color.White
            };
            AnadirAncho(pila, txtComentario);
            // Se queda con lo escrito al cerrar el panel, salvo con Esc
            FormClosing += (s, e) =>
            {
                if (!Cancelado && txtComentario.Text != comentarioInicial) CambioComentario = true;
                Comentario = txtComentario.Text;
            };
        }

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

        if (txtComentario != null)
        {
            var guardar = Tema.BotonSecundario("Guardar comentario", AnchoDoble);
            guardar.Height = 44;
            guardar.Margin = new Padding(0, 4, 0, 0);
            guardar.Click += (s, e) => Close();
            AnadirAncho(pila, guardar);
        }

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
