namespace GPE.Escritorio;

// Recuadro pequeño abajo a la derecha de la ventana principal. No coge el foco y no hay que cerrarlo.
// Normalmente se quita solo a los 15 segundos (mientras el ratón está encima, espera); con
// seQuitaSolo = false se queda hasta que se pulsa "Ver" o la ✕.
public class FormAvisoEsquina : Form
{
    private const int SegundosVisible = 15;
    private readonly System.Windows.Forms.Timer? temporizador;
    private Form? principal;

    public event EventHandler? VerPulsado;
    public event EventHandler? CerrarPulsado;

    protected override bool ShowWithoutActivation => true;

    public FormAvisoEsquina(string titulo, string texto, bool seQuitaSolo = true, string icono = "⚠")
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
            Text = icono + "  " + titulo,
            AutoSize = true,
            Font = Tema.FuenteNegrita,
            ForeColor = Tema.Aviso,
            Location = new Point(18, 14)
        };
        var lblTexto = new Label
        {
            Text = texto,
            AutoSize = false,
            Size = new Size(360, 40),
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
        btnCerrar.Click += (s, e) =>
        {
            Close();
            CerrarPulsado?.Invoke(this, EventArgs.Empty);
        };

        Controls.AddRange(new Control[] { lblTitulo, lblTexto, lnkVer, btnCerrar });

        if (seQuitaSolo)
        {
            temporizador = new() { Interval = SegundosVisible * 1000 };
            // Con el ratón encima no se quita
            temporizador.Tick += (s, e) =>
            {
                if (Bounds.Contains(Cursor.Position)) return;
                Close();
            };
            Shown += (s, e) => temporizador.Start();
        }
        FormClosed += (s, e) =>
        {
            temporizador?.Dispose();
            if (principal != null)
            {
                principal.LocationChanged -= Colocar;
                principal.SizeChanged -= Colocar;
            }
        };
    }

    // Borde del color del aviso
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        using var borde = new Pen(Tema.Aviso);
        e.Graphics.DrawRectangle(borde, 0, 0, Width - 1, Height - 1);
    }

    // Abajo a la derecha de la ventana principal, sin quitarle el foco.
    // Si la ventana se mueve o cambia de tamaño, el aviso la sigue.
    public void MostrarEn(Form principal)
    {
        this.principal = principal;
        Colocar(this, EventArgs.Empty);
        principal.LocationChanged += Colocar;
        principal.SizeChanged += Colocar;
        Show(principal);
    }

    private void Colocar(object? sender, EventArgs e)
    {
        if (principal == null || principal.WindowState == FormWindowState.Minimized) return;
        var esquina = principal.PointToScreen(new Point(principal.ClientSize.Width, principal.ClientSize.Height));
        Location = new Point(esquina.X - Width - 20, esquina.Y - Height - 20);
    }
}
