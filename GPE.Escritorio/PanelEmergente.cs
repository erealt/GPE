namespace GPE.Escritorio;

// Ventanita junto a una casilla, para usar con el dedo. Se cierra al pulsar fuera o con Esc,
// por eso se abre con Show y no con ShowDialog.
public class PanelEmergente : Form
{
    // Ancho de los botones grandes; dos juntos con su separación miden AnchoDoble
    protected const int AnchoBoton = 170;
    protected const int AnchoDoble = AnchoBoton * 2 + 8;

    // Se cerró con Esc: la pantalla no debe cambiar nada
    protected bool Cancelado { get; private set; }

    public PanelEmergente()
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.White;
        Font = Tema.Fuente;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(16, 12, 16, 14);
        KeyPreview = true;

        bool cerrando = false;
        FormClosing += (s, e) => cerrando = true;
        Deactivate += (s, e) => { if (!cerrando) Close(); };
        KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Escape) return;
            Cancelado = true;
            Close();
        };
        Paint += (s, e) =>
        {
            using var borde = new Pen(Tema.Borde);
            e.Graphics.DrawRectangle(borde, 0, 0, Width - 1, Height - 1);
        };
    }

    // Sin salirse de la pantalla; al cerrarse llama a alCerrar y se libera
    public void Abrir(Control dueno, Point donde, Action alCerrar)
    {
        var area = Screen.FromPoint(donde).WorkingArea;
        var tamano = GetPreferredSize(Size.Empty);
        Location = new Point(
            Math.Clamp(donde.X, area.Left, Math.Max(area.Left, area.Right - tamano.Width)),
            donde.Y + tamano.Height <= area.Bottom ? donde.Y : Math.Max(area.Top, donde.Y - tamano.Height - 40));

        FormClosed += (s, e) =>
        {
            alCerrar();
            dueno.BeginInvoke(Dispose);
        };
        Show(dueno.FindForm());
    }

    // Título en negrita arriba del panel
    protected static Label Titulo(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        MaximumSize = new Size(AnchoDoble + 8, 0),
        Font = Tema.FuenteNegrita,
        ForeColor = Tema.Texto,
        Margin = new Padding(0, 0, 0, 10)
    };

    protected static Label Nota(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        MaximumSize = new Size(AnchoDoble + 8, 0),
        ForeColor = Tema.Suave,
        Font = Tema.FuentePequena,
        Margin = new Padding(0, 0, 0, 10)
    };

    // Rejilla de dos columnas de botones; lo que va a lo ancho se añade con AnadirAncho
    protected static TableLayoutPanel Rejilla()
    {
        var rejilla = new TableLayoutPanel { AutoSize = true, ColumnCount = 2, Margin = new Padding(0) };
        rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AnchoBoton + 8));
        rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, AnchoBoton + 8));
        return rejilla;
    }

    protected static void AnadirAncho(TableLayoutPanel rejilla, Control control)
    {
        rejilla.Controls.Add(control);
        rejilla.SetColumnSpan(control, 2);
    }

    // Botón grande de una opción; con elegido, un borde del color del texto
    protected static Button BotonGrande(string texto, Color fondo, Color colorTexto, bool elegido = false, int ancho = AnchoBoton)
    {
        var boton = new Button
        {
            Text = texto,
            Size = new Size(ancho, 56),
            Margin = new Padding(0, 0, 8, 8),
            FlatStyle = FlatStyle.Flat,
            BackColor = fondo,
            ForeColor = colorTexto,
            Font = Tema.FuenteNegrita,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Cursor = Cursors.Hand
        };
        boton.FlatAppearance.BorderSize = elegido ? 2 : 0;
        boton.FlatAppearance.BorderColor = colorTexto;
        boton.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(fondo, 0.05f);
        return boton;
    }
}
