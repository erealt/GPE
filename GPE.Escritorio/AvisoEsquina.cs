namespace GPE.Escritorio;

// Recuadro pequeño abajo a la derecha de la ventana principal. No coge el foco, no hay que cerrarlo
// y se quita solo a los 15 segundos (mientras el ratón está encima, espera).
public class FormAvisoEsquina : Form
{
    private const int SegundosVisible = 15;
    private readonly System.Windows.Forms.Timer temporizador = new() { Interval = SegundosVisible * 1000 };

    public event EventHandler? VerPulsado;

    protected override bool ShowWithoutActivation => true;

    public FormAvisoEsquina(string titulo, string texto)
    {
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        Size = new Size(400, 120);
        BackColor = Tema.AvisoFondo;
        Font = Tema.Fuente;
        Padding = new Padding(18, 14, 12, 12);

        var lblTitulo = new Label
        {
            Text = "⚠  " + titulo,
            AutoSize = true,
            Font = Tema.FuenteNegrita,
            ForeColor = Tema.Aviso,
            Location = new Point(18, 14)
        };
        var lblTexto = new Label
        {
            Text = texto,
            AutoSize = false,
            Size = new Size(330, 40),
            ForeColor = Tema.Texto,
            Location = new Point(18, 42)
        };
        var lnkVer = new LinkLabel
        {
            Text = "Ver",
            AutoSize = true,
            Font = Tema.FuenteNegrita,
            LinkColor = Tema.Aviso,
            ActiveLinkColor = Tema.Aviso,
            Location = new Point(18, 86)
        };
        var btnCerrar = new Label
        {
            Text = "✕",
            AutoSize = true,
            ForeColor = Tema.Aviso,
            Cursor = Cursors.Hand,
            Location = new Point(Width - 30, 10)
        };

        lnkVer.LinkClicked += (s, e) =>
        {
            Close();
            VerPulsado?.Invoke(this, EventArgs.Empty);
        };
        btnCerrar.Click += (s, e) => Close();

        Controls.AddRange(new Control[] { lblTitulo, lblTexto, lnkVer, btnCerrar });

        // Con el ratón encima no se quita
        temporizador.Tick += (s, e) =>
        {
            if (Bounds.Contains(Cursor.Position)) return;
            Close();
        };
        Shown += (s, e) => temporizador.Start();
        FormClosed += (s, e) => temporizador.Dispose();
    }

    // Borde del color del aviso
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var borde = new Pen(Tema.Aviso);
        e.Graphics.DrawRectangle(borde, 0, 0, Width - 1, Height - 1);
    }

    // Abajo a la derecha de la ventana principal, sin quitarle el foco
    public void MostrarEn(Form principal)
    {
        var esquina = principal.PointToScreen(new Point(principal.ClientSize.Width, principal.ClientSize.Height));
        Location = new Point(esquina.X - Width - 20, esquina.Y - Height - 20);
        Show(principal);
    }
}
