namespace GPE.Escritorio;

// Como ElegirOpcion, pero se pueden marcar varias: cada botón se marca o desmarca al pulsarlo.
// Lo marcado vale al pulsar "Listo" o fuera del panel; con Esc no cambia nada.
public class ElegirVarios : PanelEmergente
{
    private const int AltoOpcion = 52;

    private readonly List<string> marcadas;

    // En el orden de las opciones; null si se cerró con Esc
    public List<string>? Elegidas => Cancelado ? null : marcadas;

    public ElegirVarios(string titulo, IReadOnlyList<string> opciones, IEnumerable<string> actuales)
    {
        marcadas = opciones.Where(o => actuales.Contains(o, StringComparer.OrdinalIgnoreCase)).ToList();
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
        pila.Controls.Add(Nota("Pulsa cada uno para marcarlo o desmarcarlo."));

        var lista = new FlowLayoutPanel { Width = anchoLista, Margin = new Padding(0), Padding = new Padding(0) };
        foreach (var opcion in opciones)
        {
            var boton = BotonGrande(opcion, Tema.CabeceraTabla, Tema.Texto);
            boton.Height = AltoOpcion;
            boton.Font = Tema.FuentePequenaNegrita;   // hay nombres largos: caben en dos líneas
            Pintar(boton, marcadas.Contains(opcion));
            string nombre = opcion;
            boton.Click += (s, e) =>
            {
                bool marcar = !marcadas.Remove(nombre);
                if (marcar)
                {
                    marcadas.Add(nombre);
                    marcadas.Sort((a, b) => IndiceDe(opciones, a).CompareTo(IndiceDe(opciones, b)));
                }
                Pintar(boton, marcar);
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

        var listo = Tema.BotonPrincipal("Listo", AnchoDoble);
        listo.Height = 44;
        listo.Margin = new Padding(0, 6, 0, 0);
        listo.Click += (s, e) => Close();
        pila.Controls.Add(listo);

        Controls.Add(pila);
    }

    private static int IndiceDe(IReadOnlyList<string> opciones, string opcion)
    {
        for (int i = 0; i < opciones.Count; i++)
            if (opciones[i] == opcion) return i;
        return -1;
    }

    // Marcado: con el color de acento y un ✓ delante
    private static void Pintar(Button boton, bool marcado)
    {
        string nombre = boton.Text.StartsWith("✓ ") ? boton.Text[2..] : boton.Text;
        boton.Text = marcado ? "✓ " + nombre : nombre;
        boton.BackColor = marcado ? Tema.AcentoSuave : Tema.CabeceraTabla;
        boton.ForeColor = marcado ? Tema.Acento : Tema.Texto;
        boton.FlatAppearance.BorderSize = marcado ? 2 : 0;
        boton.FlatAppearance.BorderColor = boton.ForeColor;
        boton.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(boton.BackColor, 0.05f);
    }
}
