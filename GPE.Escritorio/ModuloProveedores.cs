using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Lista de proveedores de la evaluación de proveedores y fraude (Excel CP-RG05, hoja PRODUCTOS).
public class ModuloProveedores : UserControl
{
    private const int ColumnaNumero = 0;
    private const int ColumnaEvaluacion = 3;
    private const int ColumnaProductos = 4;
    private const int ColumnaRiesgo = 5;
    private const int ColumnaRevisado = 6;
    private const int ColumnaRevisar = 7;

    private const string Todos = "Todos";
    private const string SinNota = "Sin nota";
    private const string EstadoActivos = "Activos";
    private const string EstadoRevisar = "Por revisar";
    private const string EstadoBaja = "De baja";

    private readonly ProveedorServicio servicio = new();
    private List<ProveedorConProductos> registros = new();
    private bool limpiando;

    private readonly TextBox txtBuscar = new();
    private readonly ComboBox cmbTipo = new();
    private readonly ComboBox cmbEvaluacion = new();
    private readonly ComboBox cmbRiesgo = new();
    private readonly ComboBox cmbEstado = new();
    private readonly Button btnLimpiar = Tema.BotonSecundario("Limpiar filtros", 140);
    private readonly Label lblContador = new();
    private readonly DataGridView grid = new();
    private readonly Label lblVacio = new();

    public ModuloProveedores()
    {
        Dock = DockStyle.Fill;
        Controls.Add(CrearContenido());
        Load += (s, e) => CargarDatos();
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

    private static Control CrearCabecera()
    {
        var titulos = new Panel { Dock = DockStyle.Fill };
        var lblSubtitulo = new Label
        {
            Text = "Evaluación de proveedores y fraude (CP-RG05).",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Suave
        };
        var lblTitulo = new Label
        {
            Text = "Proveedores",
            Dock = DockStyle.Top,
            Height = 50,
            Font = Tema.FuenteTitulo,
            ForeColor = Tema.Texto
        };
        titulos.Controls.Add(lblSubtitulo);
        titulos.Controls.Add(lblTitulo);
        return titulos;
    }

    private Control CrearFiltros()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15, 8, 15, 0)
        };

        txtBuscar.Width = 240;
        txtBuscar.PlaceholderText = "Nombre, Nº, código o producto...";

        foreach (var combo in new[] { cmbTipo, cmbEvaluacion, cmbRiesgo, cmbEstado })
            combo.DropDownStyle = ComboBoxStyle.DropDownList;

        cmbTipo.Width = 160;
        cmbTipo.Items.Add(Todos);
        cmbTipo.Items.AddRange(TipoProveedor.Todos);
        cmbTipo.SelectedIndex = 0;

        cmbEvaluacion.Width = 110;
        cmbEvaluacion.Items.AddRange(new object[] { "Todas", "A", "B", "C", SinNota });
        cmbEvaluacion.SelectedIndex = 0;

        cmbRiesgo.Width = 130;
        cmbRiesgo.Items.Add(Todos);
        cmbRiesgo.Items.AddRange(ProveedorServicio.Riesgos.Reverse().ToArray());
        cmbRiesgo.SelectedIndex = 0;

        cmbEstado.Width = 130;
        cmbEstado.Items.AddRange(new object[] { EstadoActivos, EstadoRevisar, EstadoBaja, Todos });
        cmbEstado.SelectedItem = EstadoActivos;   // lo del día a día son los que siguen activos

        panel.Controls.Add(Componentes.CampoFiltro("Buscar", txtBuscar));
        panel.Controls.Add(Componentes.CampoFiltro("Tipo", cmbTipo));
        panel.Controls.Add(Componentes.CampoFiltro("Evaluación", cmbEvaluacion));
        panel.Controls.Add(Componentes.CampoFiltro("Riesgo de fraude", cmbRiesgo));
        panel.Controls.Add(Componentes.CampoFiltro("Estado", cmbEstado));

        // Los filtros se aplican al momento, sin botón de "Filtrar"
        txtBuscar.TextChanged += (s, e) => AplicarFiltros();
        foreach (var combo in new[] { cmbTipo, cmbEvaluacion, cmbRiesgo, cmbEstado })
            combo.SelectedIndexChanged += (s, e) => AplicarFiltros();

        return panel;
    }

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

        btnLimpiar.Height = 34;
        btnLimpiar.Anchor = AnchorStyles.Right;
        btnLimpiar.Margin = new Padding(0);
        btnLimpiar.Click += (s, e) => LimpiarFiltros();

        barra.Controls.Add(lblContador, 0, 0);
        barra.Controls.Add(btnLimpiar, 1, 0);
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
        grid.ShowCellToolTips = true;
        grid.CellPainting += PintarEtiquetas;
        grid.SortCompare += CompararParaOrdenar;

        grid.ColumnHeadersHeight = 45;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);

        // Cada columna tiene su ancho mínimo; si sobra sitio, se reparte entre todas (si se lo queda
        // solo el nombre, que es corto, queda un hueco grande hasta el tipo)
        (string Nombre, int Ancho)[] columnas =
        {
            ("Nº", 70), ("NOMBRE", 380), ("TIPO", 175), ("EVALUACIÓN", 120), ("PRODUCTOS", 110),
            ("RIESGO FRAUDE", 165), ("REVISADO", 105), ("REVISAR", 120)
        };
        foreach (var (nombre, ancho) in columnas)
        {
            int i = grid.Columns.Add(nombre, nombre);
            grid.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns[i].MinimumWidth = ancho;
            grid.Columns[i].FillWeight = ancho;
        }
        // Al nombre le toca algo más del sitio que sobra: hay nombres largos (INCOLTEC - INNOVATION COLOUER...)
        grid.Columns[1].FillWeight = 700;

        grid.Columns[ColumnaNumero].DefaultCellStyle.ForeColor = Tema.Acento;
        grid.Columns[ColumnaNumero].DefaultCellStyle.Font = Tema.FuenteNegrita;

        return grid;
    }

    // Evaluación, riesgo y "Revisar" se dibujan como etiquetas de color
    private void PintarEtiquetas(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Graphics == null) return;
        if (grid.Rows[e.RowIndex].Tag is not ProveedorConProductos p) return;

        string? texto = e.ColumnIndex switch
        {
            ColumnaEvaluacion => ProveedorServicio.Evaluacion(p.Proveedor),
            ColumnaRiesgo => ProveedorServicio.RiesgoFraudeMasAlto(p),
            ColumnaRevisar => p.Proveedor.Revisar != null ? "⚠ Revisar" : null,
            _ => null
        };
        if (texto == null) return;

        var (fondo, color) = e.ColumnIndex == ColumnaRevisar
            ? (Tema.AvisoFondo, Tema.Aviso)
            : Tema.ColorNotaProveedor(texto);
        Componentes.PintarEtiqueta(e, texto, fondo, color);
    }

    // Nº, productos, riesgo y fecha se ordenan por el dato de verdad, no por cómo se ve
    private void CompararParaOrdenar(object? sender, DataGridViewSortCompareEventArgs e)
    {
        if (grid.Rows[e.RowIndex1].Tag is not ProveedorConProductos a || grid.Rows[e.RowIndex2].Tag is not ProveedorConProductos b)
            return;

        static int Vacios<T>(T? x, T? y, Func<T, T, int> comparar) where T : struct =>
            x == null ? (y == null ? 0 : 1) : y == null ? -1 : comparar(x.Value, y.Value);

        int? resultado = e.Column.Index switch
        {
            ColumnaNumero => Vacios(a.Proveedor.Numero, b.Proveedor.Numero, (x, y) => x.CompareTo(y)),
            ColumnaProductos => Activos(a).CompareTo(Activos(b)),
            ColumnaRiesgo => Array.IndexOf(ProveedorServicio.Riesgos, ProveedorServicio.RiesgoFraudeMasAlto(a))
                .CompareTo(Array.IndexOf(ProveedorServicio.Riesgos, ProveedorServicio.RiesgoFraudeMasAlto(b))),
            ColumnaRevisado => Vacios(a.Proveedor.FechaRevision, b.Proveedor.FechaRevision, (x, y) => x.CompareTo(y)),
            _ => null
        };
        if (resultado == null) return;

        e.SortResult = resultado.Value != 0 ? resultado.Value : string.Compare(a.Proveedor.Nombre, b.Proveedor.Nombre, StringComparison.CurrentCultureIgnoreCase);
        e.Handled = true;
    }

    private static int Activos(ProveedorConProductos p) => p.Productos.Count(x => !x.Baja);

    // Datos y filtros

    private void CargarDatos()
    {
        try
        {
            registros = servicio.ObtenerProveedores();
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudieron cargar los proveedores de la base de datos.", ex);
            return;
        }

        // Tipos escritos de otra forma en el Excel, para poder filtrar por ellos
        foreach (string tipo in registros.Select(r => r.Proveedor.Tipo).OfType<string>().Distinct())
            if (!cmbTipo.Items.Contains(tipo))
                cmbTipo.Items.Add(tipo);

        AplicarFiltros();
    }

    private static DataGridViewRow CrearFila(DataGridView grid, ProveedorConProductos p)
    {
        var fila = new DataGridViewRow { Tag = p };
        var prov = p.Proveedor;
        int activos = Activos(p);
        int deBaja = p.Productos.Count - activos;

        fila.CreateCells(grid,
            prov.Numero?.ToString() ?? "—",
            prov.Nombre,
            prov.Tipo ?? "—",
            ProveedorServicio.Evaluacion(prov) ?? "—",
            activos.ToString(),
            ProveedorServicio.RiesgoFraudeMasAlto(p) ?? "—",
            prov.FechaRevision?.ToString("dd/MM/yy") ?? "—",
            "");

        if (prov.Baja)
        {
            fila.DefaultCellStyle.ForeColor = Tema.Suave;
            fila.Cells[1].Value = prov.Nombre + "  (baja)";
        }

        string? evaluacion = ProveedorServicio.Evaluacion(prov);
        fila.Cells[ColumnaEvaluacion].ToolTipText = evaluacion == null
            ? "Falta el impacto, el estatus o las reclamaciones"
            : $"Impacto {prov.Impacto?.ToString() ?? "—"} + estatus {prov.Estatus?.ToString() ?? "—"} + reclamaciones {prov.Reclamaciones?.ToString() ?? "—"}"
              + $"\n{ProveedorServicio.QueSupone(evaluacion)}";
        if (deBaja > 0)
            fila.Cells[ColumnaProductos].ToolTipText = $"Y {deBaja} de baja";
        fila.Cells[ColumnaRiesgo].ToolTipText = "El más alto de sus productos";
        if (prov.Revisar != null)
            fila.Cells[ColumnaRevisar].ToolTipText = "En el Excel había datos distintos según la fila:\n" + prov.Revisar;

        return fila;
    }

    private void AplicarFiltros()
    {
        if (limpiando) return;
        var visibles = Filtrados();

        grid.Rows.Clear();
        grid.Rows.AddRange(visibles.Select(p => CrearFila(grid, p)).ToArray());
        // Al rehacer la tabla se mantiene la columna por la que se había ordenado
        if (grid.SortedColumn != null && grid.SortOrder != SortOrder.None)
            grid.Sort(grid.SortedColumn, grid.SortOrder == SortOrder.Ascending
                ? System.ComponentModel.ListSortDirection.Ascending
                : System.ComponentModel.ListSortDirection.Descending);
        grid.ClearSelection();

        ActualizarContador(visibles);
    }

    private List<ProveedorConProductos> Filtrados()
    {
        string texto = txtBuscar.Text.Trim();
        string? tipo = cmbTipo.SelectedIndex > 0 ? cmbTipo.Text : null;
        string? evaluacion = cmbEvaluacion.SelectedIndex > 0 ? cmbEvaluacion.Text : null;
        string? riesgo = cmbRiesgo.SelectedIndex > 0 ? cmbRiesgo.Text : null;
        string estado = cmbEstado.Text;

        // Sin mirar mayúsculas ni tildes: "azúcar" encuentra "AZUCAR BLANQUILLA"
        var comparar = System.Globalization.CultureInfo.CurrentCulture.CompareInfo;
        var opciones = System.Globalization.CompareOptions.IgnoreCase | System.Globalization.CompareOptions.IgnoreNonSpace;
        bool Contiene(string? campo) => campo != null && comparar.IndexOf(campo, texto, opciones) >= 0;

        return registros.Where(p =>
                (texto == "" || Contiene(p.Proveedor.Nombre) || p.Proveedor.Numero?.ToString() == texto
                    || p.Productos.Any(x => Contiene(x.Codigo) || Contiene(x.Descripcion)))
                && (tipo == null || p.Proveedor.Tipo == tipo)
                && (evaluacion == null
                    || (evaluacion == SinNota ? ProveedorServicio.Evaluacion(p.Proveedor) == null
                                              : ProveedorServicio.Evaluacion(p.Proveedor) == evaluacion))
                && (riesgo == null || ProveedorServicio.RiesgoFraudeMasAlto(p) == riesgo)
                && estado switch
                {
                    EstadoActivos => !p.Proveedor.Baja,
                    EstadoRevisar => p.Proveedor.Revisar != null,
                    EstadoBaja => p.Proveedor.Baja,
                    _ => true
                })
            .ToList();
    }

    private void LimpiarFiltros()
    {
        // Cada control que se limpia lanzaría AplicarFiltros: se rehace la tabla una sola vez al final
        limpiando = true;
        txtBuscar.Clear();
        cmbTipo.SelectedIndex = 0;
        cmbEvaluacion.SelectedIndex = 0;
        cmbRiesgo.SelectedIndex = 0;
        cmbEstado.SelectedItem = EstadoActivos;
        limpiando = false;
        AplicarFiltros();
    }

    private void ActualizarContador(List<ProveedorConProductos> visibles)
    {
        int productos = visibles.Sum(Activos);
        int revisar = visibles.Count(p => p.Proveedor.Revisar != null);
        lblContador.Text = $"{visibles.Count} {(visibles.Count == 1 ? "proveedor" : "proveedores")}"
            + $"  ·  {productos} {(productos == 1 ? "producto" : "productos")}"
            + (revisar > 0 ? $"  ·  {revisar} por revisar" : "");

        lblVacio.Text = registros.Count == 0
            ? "Todavía no hay proveedores. Se pasan del Excel con BaseDatos\\importar_proveedores.ps1."
            : "No hay proveedores que coincidan con los filtros.";
        lblVacio.Visible = visibles.Count == 0;
        grid.Visible = visibles.Count > 0;
    }
}
