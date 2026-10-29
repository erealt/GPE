namespace GPE.Escritorio;

// Pestañas con las fases de un registro de calidad (registro, acción inmediata, análisis...):
// un botón por fase, pegados. La elegida (la que se ve en la ficha), en morado; las
// hechas llevan ✓ y las opcionales van en gris. Al pulsar una se avisa con FasePulsada.
public class BarraFasesCalidad : FlowLayoutPanel
{
    private readonly List<Button> botones = new();
    private List<(string Texto, bool Hecha, bool Opcional)> fases = new();
    private int elegida;

    // Posición (0, 1...) de la fase pulsada
    public event Action<int>? FasePulsada;

    public BarraFasesCalidad()
    {
        WrapContents = false;
        BackColor = Color.White;
        Margin = new Padding(0);
    }

    public void Mostrar(IEnumerable<(string Texto, bool Hecha, bool Opcional)> fases)
    {
        this.fases = fases.ToList();

        // Los botones se crean una vez; luego solo cambian sus textos y colores
        while (botones.Count < this.fases.Count)
        {
            int indice = botones.Count;
            var boton = new Button
            {
                FlatStyle = FlatStyle.Flat,
                Size = new Size(190, 40),
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                TabStop = false
            };
            boton.Click += (s, e) => { if (indice != elegida) FasePulsada?.Invoke(indice); };
            botones.Add(boton);
            Controls.Add(boton);
        }
        Pintar();
    }

    public void Elegir(int fase)
    {
        elegida = fase;
        Pintar();
    }

    private void Pintar()
    {
        for (int i = 0; i < botones.Count; i++)
        {
            // Con la condición y no con Visible, que da false mientras la ventana no se ha enseñado
            var boton = botones[i];
            boton.Visible = i < fases.Count;
            if (i >= fases.Count) continue;

            var (texto, hecha, opcional) = fases[i];
            bool vista = i == elegida;

            boton.Text = (hecha ? "✓ " : "") + texto + (opcional && !hecha ? " " : "");
            boton.BackColor = vista ? Tema.Acento : Color.White;
            boton.ForeColor = vista ? Color.White : opcional ? Tema.Suave : Tema.Texto;
            boton.Font = Tema.FuenteDeOpcion(vista);
            boton.FlatAppearance.BorderColor = vista ? Tema.Acento : Tema.Borde;
            boton.FlatAppearance.MouseOverBackColor = vista ? Tema.Acento : Tema.AcentoSuave;
        }
    }
}
