namespace GPE.Escritorio;

// Opciones a la vista como botones (como el tipo de registro de la ficha NC): se elige una con un clic.
// La primera es "Sin indicar" (valor null). Si no caben en una fila, pasan a la siguiente.
public class SelectorOpciones : FlowLayoutPanel
{
    // Solo cuando la persona elige otra opción, no al llenarlo
    public event EventHandler? Cambiado;

    public string? Valor { get; private set; }

    public SelectorOpciones()
    {
        WrapContents = true;
        Margin = new Padding(0);
    }

    // El alto, con el ancho que tiene de verdad (con AutoSize se calcula como si fuera estrecho
    // y deja un hueco debajo)
    protected override void OnLayout(LayoutEventArgs levent)
    {
        base.OnLayout(levent);
        int alto = Controls.Count == 0 ? 0 : Controls.Cast<Control>().Max(c => c.Bottom + c.Margin.Bottom);
        if (Height != alto) Height = alto;
    }

    public void Mostrar(IEnumerable<string> opciones, string? elegida)
    {
        Valor = elegida;
        SuspendLayout();
        foreach (Control boton in Controls.Cast<Control>().ToList())
            boton.Dispose();
        foreach (string? opcion in opciones.Prepend(null))
            Controls.Add(NuevoBoton(opcion));
        ResumeLayout();
    }

    private RadioButton NuevoBoton(string? opcion)
    {
        var boton = new RadioButton
        {
            Text = opcion ?? "Sin indicar",
            Tag = opcion,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            FlatAppearance = { BorderColor = Tema.Borde, CheckedBackColor = Tema.Acento },
            TextAlign = ContentAlignment.MiddleCenter,
            AutoSize = true,
            MinimumSize = new Size(110, 34),
            Padding = new Padding(10, 0, 10, 0),
            Margin = new Padding(0, 0, 6, 6),
            Cursor = Cursors.Hand,
            Checked = opcion == Valor
        };
        Pintar(boton);
        boton.CheckedChanged += (s, e) =>
        {
            Pintar(boton);
            if (!boton.Checked || boton.Tag as string == Valor) return;
            Valor = boton.Tag as string;
            Cambiado?.Invoke(this, EventArgs.Empty);
        };
        return boton;
    }

    private static void Pintar(RadioButton boton)
    {
        boton.BackColor = boton.Checked ? Tema.Acento : Color.White;
        boton.ForeColor = boton.Checked ? Color.White : Tema.Texto;
        boton.Font = Tema.FuenteDeOpcion(boton.Checked);
        boton.FlatAppearance.BorderColor = boton.Checked ? Tema.Acento : Tema.Borde;
        boton.FlatAppearance.MouseOverBackColor = boton.Checked ? Tema.Acento : Tema.AcentoSuave;
    }
}
