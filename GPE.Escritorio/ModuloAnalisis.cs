using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Módulo Análisis: el plan de análisis de calidad (Excel PLAN DE ANALISIS 2026, hoja MICRO & FYQ)
// como lista con filtros. Vive dentro de FormPrincipal. Igual que el módulo DeCA.
public class ModuloAnalisis : UserControl
{
    private const string Todos = "Todos";

    private readonly AnalisisServicio servicio = new();

    private readonly TextBox txtBuscar = new();
    private readonly ComboBox cmbTipo = new();
    private readonly ComboBox cmbParametro = new();
    private readonly DateTimePicker dtpDesde = new();
    private readonly DateTimePicker dtpHasta = new();
    private readonly Button btnLimpiar = Tema.BotonSecundario("Limpiar filtros", 140);
    private readonly Label lblContador = new();
    private readonly DataGridView grid = new();
    private readonly Label lblVacio = new();
    private readonly Button btnNuevo = Tema.BotonPrincipal("+  Nuevo análisis", 200);
    private readonly Button btnDuplicar = Tema.BotonSecundario("Otro parámetro del mismo lote", 250);
    private readonly Button btnAbrir = Tema.BotonSecundario("Abrir", 110);

    public ModuloAnalisis()
    {
        Dock = DockStyle.Fill;
        Controls.Add(CrearContenido());

        // Se carga la primera vez que se ve y se recarga cada vez que se vuelve al módulo
        Load += (s, e) => CargarDatos();
        VisibleChanged += (s, e) => { if (Visible && Created) CargarDatos(); };
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
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 220));

        var titulos = new Panel { Dock = DockStyle.Fill };
        titulos.Controls.Add(new Label
        {
            Text = "Plan de análisis: microbiológicos, físico-químicos, alérgenos... de producto y materias primas.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Suave
        });
        titulos.Controls.Add(new Label
        {
            Text = "Análisis",
            Dock = DockStyle.Top,
            Height = 50,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Tema.Texto
        });

        btnNuevo.Height = 46;
        btnNuevo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnNuevo.Margin = new Padding(0, 10, 0, 0);
        btnNuevo.Click += (s, e) => AbrirFormulario(null);

        cabecera.Controls.Add(titulos, 0, 0);
        cabecera.Controls.Add(btnNuevo, 1, 0);
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

        txtBuscar.Width = 300;
        txtBuscar.PlaceholderText = "Descripción, lote, detalle o resultado";

        cmbTipo.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbTipo.Width = 130;
        cmbTipo.Items.Add(Todos);
        cmbTipo.Items.AddRange(ListasAnalisis.Tipos);
        cmbTipo.SelectedIndex = 0;

        cmbParametro.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbParametro.Width = 180;
        cmbParametro.Items.Add(Todos);
        cmbParametro.Items.AddRange(ListasAnalisis.Parametros);
        cmbParametro.SelectedIndex = 0;

        foreach (var fecha in new[] { dtpDesde, dtpHasta })
        {
            fecha.Width = 150;
            fecha.Format = DateTimePickerFormat.Short;
            fecha.ShowCheckBox = true;
            fecha.Checked = false;
        }

        btnLimpiar.Height = 30;
        btnLimpiar.Margin = new Padding(0, 22, 0, 0);

        panel.Controls.Add(Componentes.CampoFiltro("Buscar", txtBuscar));
        panel.Controls.Add(Componentes.CampoFiltro("Tipo", cmbTipo));
        panel.Controls.Add(Componentes.CampoFiltro("Parámetro", cmbParametro));
        panel.Controls.Add(Componentes.CampoFiltro("Análisis desde", dtpDesde));
        panel.Controls.Add(Componentes.CampoFiltro("Análisis hasta", dtpHasta));
        panel.Controls.Add(btnLimpiar);

        // Los filtros se aplican al momento, sin botón de "Filtrar"
        txtBuscar.TextChanged += (s, e) => AplicarFiltros();
        cmbTipo.SelectedIndexChanged += (s, e) => AplicarFiltros();
        cmbParametro.SelectedIndexChanged += (s, e) => AplicarFiltros();
        dtpDesde.ValueChanged += (s, e) => AplicarFiltros();
        dtpHasta.ValueChanged += (s, e) => AplicarFiltros();
        btnLimpiar.Click += (s, e) => LimpiarFiltros();

        return panel;
    }

    private Control CrearBarraAcciones()
    {
        var barra = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = Color.White,
            Margin = new Padding(0, 10, 0, 0),
            Padding = new Padding(15, 0, 10, 0)
        };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        lblContador.Dock = DockStyle.Fill;
        lblContador.ForeColor = Tema.Suave;
        lblContador.TextAlign = ContentAlignment.MiddleLeft;

        // Un mismo lote suele llevar varios parámetros: abre uno nuevo con los datos del lote ya puestos
        btnDuplicar.Height = 34;
        btnDuplicar.Anchor = AnchorStyles.Right;
        btnDuplicar.Margin = new Padding(0, 0, 10, 0);
        btnDuplicar.Click += (s, e) =>
        {
            if (Seleccionado() is Analisis a)
                AbrirFormulario(a with { Id = 0, Parametro = null, ParametroDetalle = null, Resultado = null, Version = null });
        };

        btnAbrir.Height = 34;
        btnAbrir.Anchor = AnchorStyles.Right;
        btnAbrir.Margin = new Padding(0);
        btnAbrir.Click += (s, e) => { if (Seleccionado() is Analisis a) AbrirFormulario(a); };

        barra.Controls.Add(lblContador, 0, 0);
        barra.Controls.Add(btnDuplicar, 1, 0);
        barra.Controls.Add(btnAbrir, 2, 0);
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
        Componentes.EstiloTabla(grid, altoFila: 50);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ColumnHeadersHeight = 45;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);

        // Las columnas de la hoja MICRO & FYQ
        string[] columnas =
        {
            "TIPO", "DESCRIPCIÓN", "LOTE", "CADUCIDAD", "ANÁLISIS TOTALES",
            "PARÁMETRO", "PARÁMETRO - DETALLE", "FECHA ANÁLISIS", "RESULTADO"
        };
        foreach (string nombre in columnas)
            grid.Columns.Add(nombre, nombre);

        grid.Columns[0].MinimumWidth = 100;
        grid.Columns[0].FillWeight = 60;
        grid.Columns[1].FillWeight = 170;
        grid.Columns[1].DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grid.Columns[2].FillWeight = 110;
        grid.Columns[3].MinimumWidth = 110;
        grid.Columns[3].FillWeight = 60;
        grid.Columns[4].FillWeight = 130;
        grid.Columns[5].MinimumWidth = 140;
        grid.Columns[5].FillWeight = 80;
        grid.Columns[6].FillWeight = 110;
        grid.Columns[7].MinimumWidth = 130;
        grid.Columns[7].FillWeight = 70;
        grid.Columns[8].FillWeight = 150;

        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is Analisis a) AbrirFormulario(a);
        };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || Seleccionado() is not Analisis a) return;
            e.Handled = true;   // que Enter no baje a la fila siguiente
            AbrirFormulario(a);
        };
        grid.SelectionChanged += (s, e) => ActivarBotones();

        return grid;
    }

    // ---------- Datos y filtros ----------

    private void CargarDatos(int? seleccionar = null)
    {
        List<Analisis> lista;
        try
        {
            lista = servicio.ObtenerTodos();
        }
        catch (DbException ex)
        {
            MessageBox.Show(FindForm(),
                "No se pudieron cargar los análisis de la base de datos.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        int? elegido = seleccionar ?? Seleccionado()?.Id;

        grid.Rows.Clear();
        foreach (var a in lista)
        {
            int i = grid.Rows.Add(
                a.Tipo,
                a.Descripcion,
                a.Lote ?? "",
                Caducidad(a.Caducidad),
                a.AnalisisTotales ?? "",
                a.Parametro ?? "",
                a.ParametroDetalle ?? "",
                a.FechaAnalisis == null ? "Sin fecha" : Componentes.Fecha(a.FechaAnalisis),
                a.Resultado ?? "");
            grid.Rows[i].Tag = a;
        }

        AplicarFiltros();
        if (elegido != null) Seleccionar(elegido.Value);
    }

    // Como en el Excel: "abr 2026" si solo se sabe el mes (día 1), y si no la fecha entera
    private static string Caducidad(DateTime? fecha) => fecha switch
    {
        null => "",
        DateTime f when f.Day == 1 => f.ToString("MMM yyyy", Componentes.Es),
        DateTime f => f.ToString("dd/MM/yyyy")
    };

    private void AplicarFiltros()
    {
        string texto = txtBuscar.Text.Trim();
        string? tipo = cmbTipo.SelectedItem is string t && t != Todos ? t : null;
        string? parametro = cmbParametro.SelectedItem is string p && p != Todos ? p : null;
        DateTime? desde = dtpDesde.Checked ? dtpDesde.Value.Date : null;
        DateTime? hasta = dtpHasta.Checked ? dtpHasta.Value.Date : null;

        grid.CurrentCell = null;

        foreach (DataGridViewRow fila in grid.Rows)
        {
            if (fila.Tag is not Analisis a) continue;

            bool coincideTexto = texto == ""
                || new[] { a.Descripcion, a.Lote, a.ParametroDetalle, a.Resultado, a.AnalisisTotales }
                    .Any(v => v != null && v.Contains(texto, StringComparison.OrdinalIgnoreCase));
            bool coincideTipo = tipo == null || a.Tipo == tipo;
            bool coincideParametro = parametro == null || a.Parametro == parametro;
            bool coincideFecha = (desde == null || a.FechaAnalisis >= desde)
                              && (hasta == null || a.FechaAnalisis <= hasta);

            fila.Visible = coincideTexto && coincideTipo && coincideParametro && coincideFecha;
        }

        bool fechasCruzadas = desde != null && hasta != null && desde > hasta;
        int visibles = grid.Rows.Cast<DataGridViewRow>().Count(f => f.Visible);
        lblContador.Text = $"{visibles} análisis";

        lblVacio.Text = fechasCruzadas
            ? "La fecha \"desde\" es posterior a la fecha \"hasta\"."
            : grid.Rows.Count == 0
                ? "Todavía no hay ningún análisis. Apunta el primero con \"+ Nuevo análisis\"."
                : "No hay ningún análisis que coincida con los filtros.";
        lblVacio.ForeColor = fechasCruzadas ? Tema.Error : Tema.Suave;
        lblVacio.Visible = visibles == 0;
        grid.Visible = visibles > 0;
        ActivarBotones();
    }

    private void ActivarBotones()
    {
        bool hay = Seleccionado() != null;
        btnAbrir.Enabled = hay;
        btnDuplicar.Enabled = hay;
    }

    private void LimpiarFiltros()
    {
        txtBuscar.Clear();
        cmbTipo.SelectedIndex = 0;
        cmbParametro.SelectedIndex = 0;
        dtpDesde.Checked = false;
        dtpHasta.Checked = false;
        AplicarFiltros();
    }

    private void Seleccionar(int id)
    {
        var fila = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(f => f.Tag is Analisis a && a.Id == id);
        if (fila == null) return;
        if (!fila.Visible) LimpiarFiltros();

        grid.ClearSelection();
        grid.CurrentCell = fila.Cells[0];
        fila.Selected = true;
        grid.FirstDisplayedScrollingRowIndex = fila.Index;
    }

    private Analisis? Seleccionado() =>
        grid.SelectedRows.Cast<DataGridViewRow>()
            .FirstOrDefault(f => f.Visible && f.Tag is Analisis)?.Tag as Analisis;

    // null = nuevo; Id 0 = nuevo con datos ya puestos
    private void AbrirFormulario(Analisis? analisis)
    {
        var descripciones = grid.Rows.Cast<DataGridViewRow>()
            .Select(f => (f.Tag as Analisis)?.Descripcion).OfType<string>();

        using var formulario = new FormAnalisis(analisis, descripciones);
        bool guardado = formulario.ShowDialog(FindForm()) == DialogResult.OK;
        CargarDatos(guardado ? formulario.Guardado?.Id : analisis?.Id);
    }
}
