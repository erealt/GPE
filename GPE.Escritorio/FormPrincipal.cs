using System.Globalization;

namespace GPE.Escritorio;

public class FormPrincipal : Form
{
    // Colores 
    private static readonly Color ColorFondo = Color.FromArgb(245, 246, 244);
    private static readonly Color ColorVerde = Color.FromArgb(22, 101, 74);
    private static readonly Color ColorTexto = Color.FromArgb(20, 35, 30);
    private static readonly Color ColorSuave = Color.FromArgb(110, 120, 115);

   
    private readonly TextBox txtBuscar = new();
    private readonly ComboBox cmbEstado = new();
    private readonly ComboBox cmbCliente = new();
    private readonly DateTimePicker dtpFecha = new();
    private readonly Label lblContador = new();
    private readonly DataGridView grid = new();
    private readonly Button btnNueva = new();

    public FormPrincipal()
    {
        Text = "GPE - Devoluciones";
        Size = new Size(1200, 750);
        MinimumSize = new Size(900, 600);
        BackColor = ColorFondo;
        Font = new Font("Segoe UI", 10);

        Controls.Add(CrearContenido());
        Controls.Add(CrearBarraSuperior());

        CargarDatosDePrueba();
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
            ForeColor = ColorSuave,
            Text = DateTime.Now.ToString("d 'de' MMMM 'de' yyyy", new CultureInfo("es-ES"))
        };

        var lblUsuario = new Label
        {
            Dock = DockStyle.Right,
            Width = 50,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = ColorVerde,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Text = "BM"
        };

        barra.Controls.Add(lblFecha);
        barra.Controls.Add(lblUsuario); // el último añadido queda más a la derecha
        return barra;
    }

    private Panel CrearContenido()
    {
        var contenido = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(40, 30, 40, 20)
        };

        // 4 filas: cabecera, filtros, contador, tabla
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 65));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        lblContador.Dock = DockStyle.Fill;
        lblContador.BackColor = Color.White;
        lblContador.ForeColor = ColorSuave;
        lblContador.TextAlign = ContentAlignment.MiddleLeft;
        lblContador.Padding = new Padding(15, 0, 0, 0);

        layout.Controls.Add(CrearCabecera(), 0, 0);
        layout.Controls.Add(CrearFiltros(), 0, 1);
        layout.Controls.Add(lblContador, 0, 2);
        layout.Controls.Add(ConfigurarGrid(), 0, 3);

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

        // Título y subtítulo apilados
        var titulos = new Panel { Dock = DockStyle.Fill };
        var lblSubtitulo = new Label
        {
            Text = "Consulta y gestiona todas las devoluciones de clientes.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = ColorSuave
        };
        var lblTitulo = new Label
        {
            Text = "Devoluciones",
            Dock = DockStyle.Top,
            Height = 50,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = ColorTexto
        };
        titulos.Controls.Add(lblSubtitulo);
        titulos.Controls.Add(lblTitulo); // el último añadido queda arriba

        // Botón verde
        btnNueva.Text = "+  Nueva devolución";
        btnNueva.Size = new Size(210, 46);
        btnNueva.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnNueva.Margin = new Padding(0, 10, 0, 0);
        btnNueva.FlatStyle = FlatStyle.Flat;
        btnNueva.FlatAppearance.BorderSize = 0;
        btnNueva.BackColor = ColorVerde;
        btnNueva.ForeColor = Color.White;
        btnNueva.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        btnNueva.Cursor = Cursors.Hand;
        btnNueva.Click += (s, e) =>
{
    using var formulario = new FormNuevaDevolucion();
    if (formulario.ShowDialog(this) == DialogResult.OK)
    {
        // TODO (tuyo): recargar la lista para que aparezca la nueva devolución
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
            Padding = new Padding(15, 15, 15, 0)
        };

        txtBuscar.Width = 380;
        txtBuscar.PlaceholderText = "Buscar por nº, cliente o albarán...";

        cmbEstado.Width = 210;
        cmbEstado.DropDownStyle = ComboBoxStyle.DropDownList;

        cmbCliente.Width = 230;
        cmbCliente.DropDownStyle = ComboBoxStyle.DropDownList;

        dtpFecha.Width = 190;
        dtpFecha.Format = DateTimePickerFormat.Short;
        dtpFecha.ShowCheckBox = true;   // desmarcado = "cualquier fecha"
        dtpFecha.Checked = false;

        foreach (Control c in new Control[] { txtBuscar, cmbEstado, cmbCliente, dtpFecha })
        {
            c.Margin = new Padding(0, 0, 12, 0);
            panel.Controls.Add(c);
        }

        // Cada cambio en un filtro vuelve a filtrar la tabla
        txtBuscar.TextChanged += (s, e) => AplicarFiltros();
        cmbEstado.SelectedIndexChanged += (s, e) => AplicarFiltros();
        cmbCliente.SelectedIndexChanged += (s, e) => AplicarFiltros();
        // TODO (tuyo): filtrar también por dtpFecha (ValueChanged) contra la fecha de solicitud

        return panel;
    }

    private Control ConfigurarGrid()
    {
        grid.Dock = DockStyle.Fill;
        grid.BackgroundColor = Color.White;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Color.FromArgb(230, 232, 228);

        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.RowTemplate.Height = 50;

        // Cabecera
        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
        grid.ColumnHeadersHeight = 45;
        var cabecera = grid.ColumnHeadersDefaultCellStyle;
        cabecera.BackColor = Color.FromArgb(243, 244, 241);
        cabecera.SelectionBackColor = cabecera.BackColor;
        cabecera.ForeColor = ColorSuave;
        cabecera.SelectionForeColor = ColorSuave;
        cabecera.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        cabecera.Padding = new Padding(10, 0, 0, 0);

        // Celdas
        grid.DefaultCellStyle.ForeColor = ColorTexto;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(232, 243, 238);
        grid.DefaultCellStyle.SelectionForeColor = ColorTexto;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);

        string[] columnas =
        {
            "Nº DEVOLUCIÓN", "CLIENTE", "ALBARÁN", "MOTIVO",
            "FECHA SOLICITUD", "FECHA RECOGIDA", "FECHA LLEGADA", "ESTADO"
        };
        foreach (string nombre in columnas)
            grid.Columns.Add(nombre, nombre);

        // El número de devolución en verde y negrita
        grid.Columns[0].DefaultCellStyle.ForeColor = ColorVerde;
        grid.Columns[0].DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);

        return grid;
    }

    // ---------- Datos y filtros ----------

    private void CargarDatosDePrueba()
    {
       
        grid.Rows.Add("DEV-2026-001", "Distribuciones Rivera", "ALB-45872", "Producto dañado", "12 Mar 2026", "14 Mar 2026", "16 Mar 2026", "Abonada");
        grid.Rows.Add("DEV-2026-002", "Alimentación Sánchez", "ALB-45901", "Error en pedido", "12 Mar 2026", "15 Mar 2026", "18 Mar 2026", "En almacén");
        grid.Rows.Add("DEV-2026-003", "Supermercados Norte", "ALB-45834", "Caducidad próxima", "11 Mar 2026", "13 Mar 2026", "15 Mar 2026", "Abonada");
        grid.Rows.Add("DEV-2026-004", "Comercial del Sur", "ALB-45798", "Envase defectuoso", "10 Mar 2026", "—", "—", "Pendiente de recogida");

        // Rellenar los desplegables a partir de los datos
        cmbEstado.Items.Add("Todos los estados");
        foreach (string estado in ValoresDistintos(7))
            cmbEstado.Items.Add(estado);

        cmbCliente.Items.Add("Todos los clientes");
        foreach (string cliente in ValoresDistintos(1))
            cmbCliente.Items.Add(cliente);

        cmbEstado.SelectedIndex = 0;
        cmbCliente.SelectedIndex = 0;
        ActualizarContador();
    }

    private IEnumerable<string> ValoresDistintos(int columna)
    {
        return grid.Rows.Cast<DataGridViewRow>()
            .Select(f => f.Cells[columna].Value?.ToString() ?? "")
            .Distinct()
            .OrderBy(v => v);
    }

    private void AplicarFiltros()
    {
        string texto = txtBuscar.Text.Trim();
        string estado = cmbEstado.SelectedIndex > 0 ? cmbEstado.Text : "";
        string cliente = cmbCliente.SelectedIndex > 0 ? cmbCliente.Text : "";

        // Hay que quitar la selección para poder ocultar la fila seleccionada
        grid.CurrentCell = null;

        foreach (DataGridViewRow fila in grid.Rows)
        {
            string numero = fila.Cells[0].Value?.ToString() ?? "";
            string cli = fila.Cells[1].Value?.ToString() ?? "";
            string albaran = fila.Cells[2].Value?.ToString() ?? "";
            string est = fila.Cells[7].Value?.ToString() ?? "";

            bool coincideTexto = texto == ""
                || numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || cli.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || albaran.Contains(texto, StringComparison.OrdinalIgnoreCase);
            bool coincideEstado = estado == "" || est == estado;
            bool coincideCliente = cliente == "" || cli == cliente;

            fila.Visible = coincideTexto && coincideEstado && coincideCliente;
        }

        ActualizarContador();
    }

    private void ActualizarContador()
    {
        int visibles = grid.Rows.Cast<DataGridViewRow>().Count(f => f.Visible);
        lblContador.Text = $"{visibles} devoluciones";
    }
}