namespace GPE.Escritorio;

// Panel para elegir una opción de una lista con botones grandes (el punto de agua, la sección
// de una observación, el operario de la caja...). Con muchas opciones salen en más columnas
// y, si aun así no caben, con scroll. Las no disponibles (ya apuntadas...) salen desactivadas.
public class ElegirOpcion : PanelEmergente
{
    private const int AltoOpcion = 52;

    // La clave de la opción elegida; null si se cerró sin elegir. Quitar: se pulsó el botón
    // de quitar lo elegido (textoQuitar).
    public string? Elegida { get; private set; }
    public bool Quitar { get; private set; }

    public ElegirOpcion(string titulo, IReadOnlyList<(string Clave, string Texto, bool Disponible)> opciones,
        string? actual, string? textoQuitar = null)
    {
        int columnas = opciones.Count > 12 ? 4 : 2;
        int anchoLista = columnas * (AnchoBoton + 8);

        var pila = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0)
        };
        var lblTitulo = Titulo(titulo);
        lblTitulo.MaximumSize = new Size(anchoLista, 0);
        pila.Controls.Add(lblTitulo);

        var lista = new FlowLayoutPanel { Width = anchoLista, Margin = new Padding(0), Padding = new Padding(0) };
        foreach (var (clave, texto, disponible) in opciones)
        {
            bool esActual = clave == actual;
            var boton = esActual
                ? BotonGrande(texto, Tema.AcentoSuave, Tema.Acento, elegido: true)
                : BotonGrande(texto, Tema.CabeceraTabla, Tema.Texto);
            boton.Height = AltoOpcion;
            boton.Font = Tema.FuentePequenaNegrita;   // hay nombres largos: caben en dos líneas
            boton.Enabled = esActual || disponible;
            if (!boton.Enabled) boton.Text += "\n(ya apuntado)";
            string elegida = clave;
            boton.Click += (s, e) =>
            {
                Elegida = elegida;
                Close();
            };
            lista.Controls.Add(boton);
        }

        // Como mucho el 60 % del alto de la pantalla; lo que no quepa, con scroll
        int filas = (opciones.Count + columnas - 1) / columnas;
        int alto = filas * (AltoOpcion + 8);
        int maximo = (int)(Screen.PrimaryScreen!.WorkingArea.Height * 0.6);
        lista.Height = alto;
        if (alto > maximo)
        {
            var desplazable = new Panel
            {
                Size = new Size(anchoLista + SystemInformation.VerticalScrollBarWidth, maximo),
                AutoScroll = true,
                Margin = new Padding(0)
            };
            desplazable.Controls.Add(lista);
            pila.Controls.Add(desplazable);
        }
        else
            pila.Controls.Add(lista);

        if (textoQuitar != null)
        {
            var quitar = Tema.BotonSecundario(textoQuitar, AnchoDoble);
            quitar.Height = 44;
            quitar.Margin = new Padding(0, 6, 0, 0);
            quitar.Enabled = actual != null;
            quitar.Click += (s, e) =>
            {
                Quitar = true;
                Close();
            };
            pila.Controls.Add(quitar);
        }

        Controls.Add(pila);
    }
}
