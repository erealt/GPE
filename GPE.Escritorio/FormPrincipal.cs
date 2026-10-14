using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio;

public class FormPrincipal : Form
{
    // Colores
    private static readonly Color ColorFondo = Color.FromArgb(245, 246, 244);
    private static readonly Color ColorAzul = Color.FromArgb(89, 0, 246);
    private static readonly Color ColorTexto = Color.FromArgb(20, 35, 30);
    private static readonly Color ColorSuave = Color.FromArgb(110, 120, 115);

    private static readonly CultureInfo Es = new("es-ES");

    private readonly DevolucionServicio servicio = new();

    private readonly TextBox txtBuscar = new();
    private readonly ComboBox cmbEstado = new();
    private readonly DateTimePicker dtpDesde = new();
    private readonly DateTimePicker dtpHasta = new();
    private readonly Button btnFiltrar = new();
    private readonly Button btnLimpiar = new();
    private readonly Label lblContador = new();
    private readonly DataGridView grid = new();
    private readonly Button btnNueva = new();

    public FormPrincipal()
    {
        Text = "GPE - Devoluciones";
        Size = new Size(1200, 750);
        MinimumSize = new Size(1150, 600);
        BackColor = ColorFondo;
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
            ForeColor = ColorSuave,
            Text = DateTime.Now.ToString("d 'de' MMMM 'de' yyyy", Es)
        };

        var lblUsuario = new Label
        {
            Dock = DockStyle.Right,
            Width = 50,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = ColorAzul,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Text = Sesion.UsuarioActual?.Iniciales ?? ""
        };
        new ToolTip().SetToolTip(lblUsuario, Sesion.UsuarioActual?.Nombre);

        barra.Controls.Add(lblFecha);
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
        titulos.Controls.Add(lblTitulo); 

        // Botón principal
        btnNueva.Text = "+  Nueva devolución";
        btnNueva.Size = new Size(210, 46);
        btnNueva.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnNueva.Margin = new Padding(0, 10, 0, 0);
        btnNueva.FlatStyle = FlatStyle.Flat;
        btnNueva.FlatAppearance.BorderSize = 0;
        btnNueva.BackColor = ColorAzul;
        btnNueva.ForeColor = Color.White;
        btnNueva.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        btnNueva.Cursor = Cursors.Hand;
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

        btnFiltrar.Text = "Filtrar";
        btnFiltrar.Size = new Size(110, 30);
        btnFiltrar.Margin = new Padding(0, 22, 8, 0);
        btnFiltrar.FlatStyle = FlatStyle.Flat;
        btnFiltrar.FlatAppearance.BorderSize = 0;
        btnFiltrar.BackColor = ColorAzul;
        btnFiltrar.ForeColor = Color.White;
        btnFiltrar.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        btnFiltrar.Cursor = Cursors.Hand;

        btnLimpiar.Text = "Limpiar filtros";
        btnLimpiar.Size = new Size(130, 30);
        btnLimpiar.Margin = new Padding(0, 22, 0, 0);
        btnLimpiar.FlatStyle = FlatStyle.Flat;
        btnLimpiar.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 200);
        btnLimpiar.BackColor = Color.White;
        btnLimpiar.ForeColor = ColorTexto;
        btnLimpiar.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        btnLimpiar.Cursor = Cursors.Hand;

        panel.Controls.Add(CampoFiltro("Buscar", txtBuscar));
        panel.Controls.Add(CampoFiltro("Estado", cmbEstado));
        panel.Controls.Add(CampoFiltro("Fecha desde", dtpDesde));
        panel.Controls.Add(CampoFiltro("Fecha hasta", dtpHasta));
        panel.Controls.Add(btnFiltrar);
        panel.Controls.Add(btnLimpiar);


        btnFiltrar.Click += (s, e) => AplicarFiltros();
        btnLimpiar.Click += (s, e) => LimpiarFiltros();
        txtBuscar.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            AplicarFiltros();
            e.SuppressKeyPress = true;
        };

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
            ForeColor = ColorSuave,
            Font = new Font("Segoe UI", 9)
        };
        control.Dock = DockStyle.Top;
        campo.Controls.Add(control);
        campo.Controls.Add(lbl);
        return campo;
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

        grid.CellDoubleClick += (s, e) => AbrirDevolucion(e.RowIndex);

        // Clic derecho sobre una fila: la selecciona y muestra el menú
        var menu = new ContextMenuStrip();
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
        cabecera.BackColor = Color.FromArgb(243, 244, 241);
        cabecera.SelectionBackColor = cabecera.BackColor;
        cabecera.ForeColor = ColorSuave;
        cabecera.SelectionForeColor = ColorSuave;
        cabecera.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        cabecera.Padding = new Padding(10, 0, 0, 0);

        // Celdas
        grid.DefaultCellStyle.ForeColor = ColorTexto;
        grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(240, 236, 255);
        grid.DefaultCellStyle.SelectionForeColor = ColorTexto;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);

        string[] columnas =
        {
            "Nº DEVOLUCIÓN", "CLIENTE", "ALBARÁN", "MOTIVO",
            "FECHA SOLICITUD", "FECHA RECOGIDA", "FECHA LLEGADA", "ESTADO"
        };
        foreach (string nombre in columnas)
            grid.Columns.Add(nombre, nombre);

        
        grid.Columns[0].DefaultCellStyle.ForeColor = ColorAzul;
        grid.Columns[0].DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);

        return grid;
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

        if (desde != null && hasta != null && desde > hasta)
        {
            MessageBox.Show(this, "La fecha \"desde\" no puede ser posterior a \"hasta\".",
                "Fechas incorrectas", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }


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

            bool coincideFecha = fila.Tag is not DevolucionFila d
                || ((desde == null || d.FechaSolicitud >= desde)
                    && (hasta == null || d.FechaSolicitud <= hasta));

            fila.Visible = coincideTexto && coincideEstado && coincideFecha;
        }

        ActualizarContador();
    }

    private void LimpiarFiltros()
    {
        txtBuscar.Clear();
        cmbEstado.SelectedIndex = 0;
        dtpDesde.Checked = false;
        dtpHasta.Checked = false;
        AplicarFiltros();
    }

    private void ActualizarContador()
    {
        int visibles = grid.Rows.Cast<DataGridViewRow>().Count(f => f.Visible);
        lblContador.Text = $"{visibles} devoluciones";
    }
}