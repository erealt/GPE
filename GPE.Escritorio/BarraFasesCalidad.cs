namespace GPE.Escritorio;

// Índice de las fases de un registro de calidad: las hechas con ✓ en verde y las opcionales en gris.
// Al pulsar una, la ficha baja hasta ella.
public class BarraFasesCalidad : FlowLayoutPanel
{
    private readonly List<Button> botones = new();
    private List<(string Texto, bool Hecha, bool Opcional)> fases = new();

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
                TabStop = false,
                BackColor = Color.White,
                Font = Tema.FuenteDeOpcion(false),
                FlatAppearance = { BorderColor = Tema.Borde, MouseOverBackColor = Tema.AcentoSuave }
            };
            boton.Click += (s, e) => FasePulsada?.Invoke(indice);
            botones.Add(boton);
            Controls.Add(boton);
        }
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
            boton.Text = (hecha ? "✓ " : "") + texto + (opcional && !hecha ? " " : "");
            // Las hechas en verde, como la etiqueta de su tarjeta
            boton.BackColor = hecha ? Tema.FaseHecha.Fondo : Color.White;
            boton.ForeColor = hecha ? Tema.FaseHecha.Texto : opcional ? Tema.Suave : Tema.Texto;
            boton.FlatAppearance.BorderColor = hecha ? Tema.FaseHecha.Texto : Tema.Borde;
        }
    }
}
