using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace GPE.Escritorio;

public class FormPrincipal : Form
{
    private static readonly CultureInfo Es = new("es-ES");

    private const int ColumnaEstado = 7;

    private readonly DevolucionServicio servicio = new();

    private readonly TextBox txtBuscar = new();
    private readonly ComboBox cmbEstado = new();
    private readonly DateTimePicker dtpDesde = new();
    private readonly DateTimePicker dtpHasta = new();
    private readonly Button btnLimpiar = Tema.BotonSecundario("Limpiar filtros", 140);
    private readonly Label lblContador = new();
    private readonly DataGridView grid = new();
    private readonly Label lblVacio = new();
    private readonly Button btnNueva = Tema.BotonPrincipal("+  Nueva devolución", 210);
    private readonly Button btnAbrir = Tema.BotonPrincipal("Abrir", 110);
    private readonly Button btnHistorial = Tema.BotonSecundario("Ver historial", 140);

    public FormPrincipal()
    {
        Text = "GPE - Devoluciones";
        Size = new Size(1200, 750);
        MinimumSize = new Size(1150, 600);
        BackColor = Tema.Fondo;
        Font = new Font("Segoe UI", 10);

        Controls.Add(CrearContenido());
        Controls.Add(CrearBarraSuperior());

        Load += (s, e) => CargarDatos();
    }

    // ---------- Construcción de la interfaz ----------

    private Panel CrearBarraSuperior()
    {
        var barra = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.White,
            Padding = new Padding(0, 0, 30, 0)
        };

        var lblFecha = new Label
        {
            Dock = DockStyle.Right,
            Width = 220,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Tema.Suave,
            Text = DateTime.Now.ToString("d 'de' MMMM 'de' yyyy", Es)
        };

        // Iniciales dentro de un círculo (se dibujan a mano en Paint)
        string iniciales = Sesion.UsuarioActual?.Iniciales ?? "";
        var lblUsuario = new Label
        {
            Dock = DockStyle.Right,
            Width = 50,
            Font = new Font("Segoe UI", 10, FontStyle.Bold)
        };
        lblUsuario.Paint += (s, e) =>
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int lado = 34;
            var circulo = new Rectangle((lblUsuario.Width - lado) / 2, (lblUsuario.Height - lado) / 2, lado, lado);
            using var relleno = new SolidBrush(Tema.AcentoSuave);
            e.Graphics.FillEllipse(relleno, circulo);
            TextRenderer.DrawText(e.Graphics, iniciales, lblUsuario.Font, circulo, Tema.Acento,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        };

        var lblNombre = new Label
        {
            Dock = DockStyle.Right,
            AutoSize = false,
            Width = 200,
            TextAlign = ContentAlignment.MiddleRight,
            ForeColor = Tema.Texto,
            Text = Sesion.UsuarioActual?.Nombre ?? ""
        };

        // Con Dock = Right, el último añadido queda más a la derecha
        barra.Controls.Add(lblFecha);
        barra.Controls.Add(lblNombre);
        barra.Controls.Add(lblUsuario);
        return barra;
    }

    private Panel CrearContenido()
    {
        var contenido = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(40, 30, 40, 20)
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CrearCabecera(), 0, 0);
        layout.Controls.Add(CrearFiltros(), 0, 1);
        layout.Controls.Add(CrearBarraAcciones(), 0, 2);
        layout.Controls.Add(CrearZonaTabla(), 0, 3);

        contenido.Controls.Add(layout);
        return contenido;
    }

    private Control CrearCabecera()
    {
        var cabecera = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 230));

        var titulos = new Panel { Dock = DockStyle.Fill };
        var lblSubtitulo = new Label
        {
            Text = "Consulta y gestiona todas las devoluciones de clientes.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Suave
        };
        var lblTitulo = new Label
        {
            Text = "Devoluciones",
            Dock = DockStyle.Top,
            Height = 50,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Tema.Texto
        };
        titulos.Controls.Add(lblSubtitulo);
        titulos.Controls.Add(lblTitulo);

        // Botón principal
        btnNueva.Height = 46;
        btnNueva.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnNueva.Margin = new Padding(0, 10, 0, 0);
        btnNueva.Click += (s, e) =>
        {
            using var formulario = new FormNuevaDevolucion();
            if (formulario.ShowDialog(this) == DialogResult.OK)
            {
                CargarDatos();
                SeleccionarDevolucion(formulario.IdCreado);
            }
        };

        cabecera.Controls.Add(titulos, 0, 0);
        cabecera.Controls.Add(btnNueva, 1, 0);
        return cabecera;
    }

    private Control CrearFiltros()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15, 8, 15, 0)
        };

        txtBuscar.Width = 340;
        txtBuscar.PlaceholderText = "Nº, cliente o albarán";

        cmbEstado.Width = 210;
        cmbEstado.DropDownStyle = ComboBoxStyle.DropDownList;

        foreach (var fecha in new[] { dtpDesde, dtpHasta })
        {
            fecha.Width = 150;
            fecha.Format = DateTimePickerFormat.Short;
            fecha.ShowCheckBox = true;
            fecha.Checked = false;
        }

        btnLimpiar.Height = 30;
        btnLimpiar.Margin = new Padding(0, 22, 0, 0);

        panel.Controls.Add(CampoFiltro("Buscar", txtBuscar));
        panel.Controls.Add(CampoFiltro("Estado", cmbEstado));
        panel.Controls.Add(CampoFiltro("Fecha desde", dtpDesde));
        panel.Controls.Add(CampoFiltro("Fecha hasta", dtpHasta));
        panel.Controls.Add(btnLimpiar);

        // Los filtros se aplican al momento, sin botón de "Filtrar"
        txtBuscar.TextChanged += (s, e) => AplicarFiltros();
        cmbEstado.SelectedIndexChanged += (s, e) => AplicarFiltros();
        dtpDesde.ValueChanged += (s, e) => AplicarFiltros();
        dtpHasta.ValueChanged += (s, e) => AplicarFiltros();
        btnLimpiar.Click += (s, e) => LimpiarFiltros();

        return panel;
    }

    private static Control CampoFiltro(string etiqueta, Control control)
    {
        var campo = new Panel { Size = new Size(control.Width, 58), Margin = new Padding(0, 0, 12, 0) };
        var lbl = new Label
        {
            Text = etiqueta,
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = Tema.Suave,
            Font = new Font("Segoe UI", 9)
        };
        control.Dock = DockStyle.Top;
        campo.Controls.Add(control);
        campo.Controls.Add(lbl);
        return campo;
    }

    // Contador de resultados y acciones sobre la devolución seleccionada
    private Control CrearBarraAcciones()
    {
        var barra = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.White,
            Margin = new Padding(0, 10, 0, 0),
            Padding = new Padding(15, 0, 10, 0)
        };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        lblContador.Dock = DockStyle.Fill;
        lblContador.ForeColor = Tema.Suave;
        lblContador.TextAlign = ContentAlignment.MiddleLeft;

        var botones = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0)
        };
        btnAbrir.Height = btnHistorial.Height = 34;
        btnAbrir.Margin = new Padding(0);
        btnHistorial.Margin = new Padding(0, 0, 8, 0);
        botones.Controls.Add(btnAbrir);
        botones.Controls.Add(btnHistorial);

        btnAbrir.Click += (s, e) => AbrirDevolucion(grid.CurrentRow?.Index ?? -1);
        btnHistorial.Click += (s, e) => AbrirHistorial();

        barra.Controls.Add(lblContador, 0, 0);
        barra.Controls.Add(botones, 1, 0);
        return barra;
    }

    private Control CrearZonaTabla()
    {
        var zona = new Panel { Dock = DockStyle.Fill };

        // Se muestra en lugar de la tabla cuando no hay nada que enseñar
        lblVacio.Dock = DockStyle.Fill;
        lblVacio.BackColor = Color.White;
        lblVacio.ForeColor = Tema.Suave;
        lblVacio.TextAlign = ContentAlignment.MiddleCenter;
        lblVacio.Visible = false;

        zona.Controls.Add(lblVacio);
        zona.Controls.Add(ConfigurarGrid());
        return zona;
    }

    private Control ConfigurarGrid()
    {
        grid.Dock = DockStyle.Fill;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Tema.Linea;

        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowTemplate.Height = 50;

        grid.CellDoubleClick += (s, e) => AbrirDevolucion(e.RowIndex);
        grid.SelectionChanged += (s, e) => ActualizarBotones();
        grid.CellPainting += PintarEstado;

        // Clic derecho sobre una fila: la selecciona y muestra el menú
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir", null, (s, e) => AbrirDevolucion(grid.CurrentRow?.Index ?? -1));
        menu.Items.Add("Ver historial de cambios", null, (s, e) => AbrirHistorial());
        grid.ContextMenuStrip = menu;
        grid.CellMouseDown += (s, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            grid.CurrentCell = grid.Rows[e.RowIndex].Cells[Math.Max(e.ColumnIndex, 0)];
        };
        menu.Opening += (s, e) => e.Cancel = grid.CurrentRow?.Tag is not DevolucionFila;

        // Cabecera
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 45;
        var cabecera = grid.ColumnHeadersDefaultCellStyle;
        cabecera.BackColor = Tema.CabeceraTabla;
        cabecera.SelectionBackColor = cabecera.BackColor;
        cabecera.ForeColor = Tema.Suave;
        cabecera.SelectionForeColor = Tema.Suave;
        cabecera.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        cabecera.Padding = new Padding(10, 0, 0, 0);

        // Celdas
        grid.DefaultCellStyle.ForeColor = Tema.Texto;
        grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;
        grid.DefaultCellStyle.SelectionForeColor = Tema.Texto;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);

        string[] columnas =
        {
            "Nº DEVOLUCIÓN", "CLIENTE", "ALBARÁN", "MOTIVO",
            "FECHA SOLICITUD", "FECHA RECOGIDA", "FECHA LLEGADA", "ESTADO"
        };
        foreach (string nombre in columnas)
            grid.Columns.Add(nombre, nombre);

        grid.Columns[0].DefaultCellStyle.ForeColor = Tema.Acento;
        grid.Columns[0].DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grid.Columns[ColumnaEstado].FillWeight = 140;

        return grid;
    }

    // Dibuja el estado como una etiqueta redondeada con el color de su fase
    private void PintarEstado(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != ColumnaEstado || e.Graphics == null) return;
        if (grid.Rows[e.RowIndex].Tag is not DevolucionFila devolucion) return;

        e.PaintBackground(e.CellBounds, (e.State & DataGridViewElementStates.Selected) != 0);

        var (fondo, texto) = Tema.ColorFase(devolucion.Fase);
        using var fuente = new Font("Segoe UI", 8.5f, FontStyle.Bold);

        var medida = TextRenderer.MeasureText(devolucion.Estado, fuente);
        var etiqueta = new Rectangle(
            e.CellBounds.X + 10,
            e.CellBounds.Y + (e.CellBounds.Height - 26) / 2,
            Math.Min(medida.Width + 20, e.CellBounds.Width - 15),
            26);

        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var ruta = Redondeado(etiqueta, 13))
        using (var relleno = new SolidBrush(fondo))
            e.Graphics.FillPath(relleno, ruta);

        TextRenderer.DrawText(e.Graphics, devolucion.Estado, fuente, etiqueta, texto,
            TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

        e.Handled = true;
    }

    private static GraphicsPath Redondeado(Rectangle r, int radio)
    {
        var ruta = new GraphicsPath();
        int d = radio * 2;
        ruta.AddArc(r.X, r.Y, d, d, 180, 90);
        ruta.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        ruta.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        ruta.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        ruta.CloseFigure();
        return ruta;
    }

    // ---------- Datos y filtros ----------

    private void CargarDatos()
    {
        List<DevolucionFila> devoluciones;
        try
        {
            devoluciones = servicio.ObtenerDevoluciones();

            if (cmbEstado.Items.Count == 0)
            {
                cmbEstado.Items.Add("Todos los estados");
                foreach (string estado in servicio.ObtenerEstados())
                    cmbEstado.Items.Add(estado);
                cmbEstado.SelectedIndex = 0;
            }
        }
        catch (DbException ex)
        {
            MessageBox.Show(this,
                "No se pudieron cargar las devoluciones de la base de datos.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        grid.Rows.Clear();
        foreach (var devolucion in devoluciones)
            AnadirDevolucion(devolucion);

        AplicarFiltros();
    }

    private void AnadirDevolucion(DevolucionFila d)
    {
        int i = grid.Rows.Add(
            d.Numero,
            d.Cliente,
            d.Albaran ?? "—",
            d.Motivo,
            FormatoFecha(d.FechaSolicitud),
            FormatoFecha(d.FechaRecogida),
            FormatoFecha(d.FechaLlegada),
            d.Estado);

        grid.Rows[i].Tag = d;
    }

    private void SeleccionarDevolucion(int id)
    {
        var fila = grid.Rows.Cast<DataGridViewRow>()
            .FirstOrDefault(f => f.Tag is DevolucionFila d && d.Id == id);

        if (fila == null) return;

        if (!fila.Visible)
            LimpiarFiltros();

        grid.ClearSelection();
        grid.CurrentCell = fila.Cells[0];
        fila.Selected = true;
        grid.FirstDisplayedScrollingRowIndex = fila.Index;
    }

    private void AbrirDevolucion(int indiceFila)
    {
        if (indiceFila < 0) return;   // doble clic en la cabecera de la tabla
        if (grid.Rows[indiceFila].Tag is not DevolucionFila devolucion) return;

        switch (devolucion.Fase)
        {
            case 1:   // Solicitud recibida
                using (var pantalla = new Recogida(devolucion))
                    pantalla.ShowDialog(this);
                break;

            case 2:   // Recogida solicitada
            case 3:   // Mercancía recibida
                MessageBox.Show(this, "La pantalla de almacén todavía no está disponible.",
                    "En construcción", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;

            default:
                MessageBox.Show(this, "La pantalla de abono todavía no está disponible.",
                    "En construcción", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
        }

        CargarDatos();
        SeleccionarDevolucion(devolucion.Id);
    }

    private void AbrirHistorial()
    {
        if (grid.CurrentRow?.Tag is not DevolucionFila devolucion) return;

        using var pantalla = new FormHistorial(devolucion);
        pantalla.ShowDialog(this);
    }

    private static string FormatoFecha(DateTime? fecha) =>
        fecha?.ToString("dd MMM yyyy", Es) ?? "—";

    private void AplicarFiltros()
    {
        string texto = txtBuscar.Text.Trim();
        string estado = cmbEstado.SelectedIndex > 0 ? cmbEstado.Text : "";
        DateTime? desde = dtpDesde.Checked ? dtpDesde.Value.Date : null;
        DateTime? hasta = dtpHasta.Checked ? dtpHasta.Value.Date : null;

        grid.CurrentCell = null;

        foreach (DataGridViewRow fila in grid.Rows)
        {
            string numero = fila.Cells[0].Value?.ToString() ?? "";
            string cli = fila.Cells[1].Value?.ToString() ?? "";
            string albaran = fila.Cells[2].Value?.ToString() ?? "";
            string est = fila.Cells[ColumnaEstado].Value?.ToString() ?? "";

            bool coincideTexto = texto == ""
                || numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || cli.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || albaran.Contains(texto, StringComparison.OrdinalIgnoreCase);
            bool coincideEstado = estado == "" || est == estado;

            bool coincideFecha = fila.Tag is not DevolucionFila d
                || ((desde == null || d.FechaSolicitud >= desde)
                    && (hasta == null || d.FechaSolicitud <= hasta));

            fila.Visible = coincideTexto && coincideEstado && coincideFecha;
        }

        ActualizarContador(desde != null && hasta != null && desde > hasta);
        ActualizarBotones();
    }

    private void LimpiarFiltros()
    {
        txtBuscar.Clear();
        cmbEstado.SelectedIndex = 0;
        dtpDesde.Checked = false;
        dtpHasta.Checked = false;
        AplicarFiltros();
    }

    private void ActualizarContador(bool fechasCruzadas)
    {
        int visibles = grid.Rows.Cast<DataGridViewRow>().Count(f => f.Visible);
        lblContador.Text = $"{visibles} {(visibles == 1 ? "devolución" : "devoluciones")}";

        lblVacio.Text = fechasCruzadas
            ? "La fecha \"desde\" es posterior a la fecha \"hasta\"."
            : grid.Rows.Count == 0
                ? "Todavía no hay devoluciones. Crea la primera con \"+ Nueva devolución\"."
                : "No hay devoluciones que coincidan con los filtros.";
        lblVacio.ForeColor = fechasCruzadas ? Tema.Error : Tema.Suave;
        lblVacio.Visible = visibles == 0;
        grid.Visible = visibles > 0;
    }

    private void ActualizarBotones()
    {
        bool haySeleccion = grid.CurrentRow is { Visible: true, Tag: DevolucionFila };
        btnAbrir.Enabled = haySeleccion;
        btnHistorial.Enabled = haySeleccion;
    }
}
