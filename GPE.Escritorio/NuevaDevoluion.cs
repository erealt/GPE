namespace GPE.Escritorio;

public class FormNuevaDevolucion : Form
{
    private static readonly Color ColorFondo = Color.FromArgb(245, 246, 244);
    private static readonly Color ColorTexto = Color.FromArgb(20, 35, 30);
    private static readonly Color ColorSuave = Color.FromArgb(110, 120, 115);

    // 1. Datos de la devolución
    private readonly ComboBox cmbCliente = new();
    private readonly DateTimePicker dtpSolicitud = new();
    private readonly ComboBox cmbMotivo = new();
    private readonly TextBox txtAlbaran = new();
    private readonly TextBox txtObservaciones = new();

    public FormNuevaDevolucion()
    {
        Text = "Nueva devolución";
        Size = new Size(1000, 800);
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ColorFondo;
        Font = new Font("Segoe UI", 10);

        PrepararControles();
        Controls.Add(CrearContenido());
    }

    // ---------- Preparación de controles ----------

    private void PrepararControles()
    {
        cmbCliente.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbMotivo.DropDownStyle = ComboBoxStyle.DropDownList;

        cmbCliente.Items.AddRange(new object[]
            { "Distribuciones Rivera", "Alimentación Sánchez", "Supermercados Norte", "Comercial del Sur" });
        cmbMotivo.Items.AddRange(new object[]
            { "Producto dañado", "Error en pedido", "Caducidad próxima", "Envase defectuoso", "Otro" });

        dtpSolicitud.Format = DateTimePickerFormat.Short;   // por defecto: hoy

        txtAlbaran.PlaceholderText = "Ej. ALB-45872";
        txtObservaciones.PlaceholderText = "Añade cualquier información relevante sobre la solicitud...";
        txtObservaciones.Multiline = true;
        txtObservaciones.Height = 80;
        txtObservaciones.ScrollBars = ScrollBars.Vertical;
    }

    // ---------- Estructura general ----------

    private Panel CrearContenido()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(40, 25, 40, 25)
        };

        // Pila vertical: cada hijo es una fila que ocupa todo el ancho
        var pila = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1
        };
        pila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        pila.Controls.Add(CrearCabecera());
        pila.Controls.Add(CrearTarjetaDatos());

        scroll.Controls.Add(pila);
        return scroll;
    }

    private Control CrearCabecera()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Height = 120, Margin = new Padding(0, 0, 0, 10) };

        var lblSubtitulo = new Label
        {
            Text = "Completa los datos para iniciar el proceso de devolución.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = ColorSuave
        };
        var lblTitulo = new Label
        {
            Text = "Nueva devolución",
            Dock = DockStyle.Top,
            Height = 50,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = ColorTexto
        };
        var lblRuta = new Label
        {
            Text = "DEVOLUCIONES / NUEVA",
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = ColorSuave,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        // El último añadido queda arriba
        panel.Controls.Add(lblSubtitulo);
        panel.Controls.Add(lblTitulo);
        panel.Controls.Add(lblRuta);
        return panel;
    }

    // ---------- Tarjeta 1 ----------

    private Control CrearTarjetaDatos()
    {
        var tarjeta = CrearTarjeta("1", "Datos de la devolución", "Información general de la solicitud del cliente");

        var rejilla = Rejilla(2);
        rejilla.Controls.Add(Campo("Cliente", cmbCliente));
        rejilla.Controls.Add(Campo("Fecha de solicitud", dtpSolicitud));
        rejilla.Controls.Add(Campo("Motivo de devolución", cmbMotivo));
        rejilla.Controls.Add(Campo("Albarán relacionado", txtAlbaran));

        var campoObservaciones = Campo("Observaciones", txtObservaciones);
        rejilla.Controls.Add(campoObservaciones);
        rejilla.SetColumnSpan(campoObservaciones, 2);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // ---------- Ayudas para construir la interfaz ----------

    private TableLayoutPanel CrearTarjeta(string numero, string titulo, string subtitulo)
    {
        var tarjeta = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.White,
            Padding = new Padding(25),
            Margin = new Padding(0, 0, 0, 20)
        };
        tarjeta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var textos = new Panel { Dock = DockStyle.Fill, Height = 55, Margin = new Padding(0, 0, 0, 15) };
        var lblSubtitulo = new Label
        {
            Text = subtitulo,
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = ColorSuave,
            Font = new Font("Segoe UI", 9)
        };
        var lblTitulo = new Label
        {
            Text = $"{numero}   {titulo}",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Segoe UI", 13),
            ForeColor = ColorTexto
        };
        textos.Controls.Add(lblSubtitulo);
        textos.Controls.Add(lblTitulo);

        tarjeta.Controls.Add(textos);
        return tarjeta;
    }

    private static TableLayoutPanel Rejilla(int columnas)
    {
        var rejilla = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = columnas };
        for (int i = 0; i < columnas; i++)
            rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnas));
        return rejilla;
    }

    private static Control Campo(string etiqueta, Control control)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 15, 15)
        };
        panel.Controls.Add(new Label
        {
            Text = etiqueta,
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        });

        control.Dock = DockStyle.Top;
        panel.Controls.Add(control);
        return panel;
    }
}