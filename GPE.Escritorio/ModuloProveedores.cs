using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Lista de proveedores de la evaluación de proveedores y fraude (Excel CP-RG05, hoja PRODUCTOS).
public class ModuloProveedores : UserControl
{
    // Columnas: los datos del proveedor de la hoja (A-J), sin fecha de revisión ni certificados
    private const int ColumnaNumero = 0;
    private const int ColumnaNombre = 1;
    private const int ColumnaAlta = 2;
    private const int ColumnaEstado = 3;
    private const int ColumnaImpacto = 5;
    private const int ColumnaEstatus = 6;
    private const int ColumnaReclamaciones = 7;
    private const int ColumnaEvaluacion = 8;

    private const string Todos = "Todos";
    private const string SinNota = "Sin nota";
    private const string EstadoActivos = "Activos";
    private const string EstadoBaja = "De baja";

    private readonly ProveedorServicio servicio = new();
    private List<ProveedorConProductos> registros = new();
    private bool limpiando;

    private readonly TextBox txtBuscar = new();
    private readonly ComboBox cmbTipo = new();
    private readonly ComboBox cmbEvaluacion = new();
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

        foreach (var combo in new[] { cmbTipo, cmbEvaluacion, cmbEstado })
            combo.DropDownStyle = ComboBoxStyle.DropDownList;

        cmbTipo.Width = 160;
        cmbTipo.Items.Add(Todos);
        cmbTipo.Items.AddRange(TipoProveedor.Todos);
        cmbTipo.SelectedIndex = 0;

        cmbEvaluacion.Width = 110;
        cmbEvaluacion.Items.AddRange(new object[] { "Todas", "A", "B", "C", SinNota });
        cmbEvaluacion.SelectedIndex = 0;

        cmbEstado.Width = 130;
        cmbEstado.Items.AddRange(new object[] { EstadoActivos, EstadoBaja, Todos });
        cmbEstado.SelectedItem = EstadoActivos;   // lo del día a día son los que siguen activos

        panel.Controls.Add(Componentes.CampoFiltro("Buscar", txtBuscar));
        panel.Controls.Add(Componentes.CampoFiltro("Tipo", cmbTipo));
        panel.Controls.Add(Componentes.CampoFiltro("Evaluación", cmbEvaluacion));
        panel.Controls.Add(Componentes.CampoFiltro("Estado", cmbEstado));

        // Los filtros se aplican al momento, sin botón de "Filtrar"
        txtBuscar.TextChanged += (s, e) => AplicarFiltros();
        foreach (var combo in new[] { cmbTipo, cmbEvaluacion, cmbEstado })
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
        lblContador.ForeColor = Tema.Texto;
        lblContador.Font = Tema.FuenteNegrita;
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

        // Doble clic o Enter: ficha del proveedor con sus productos
        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is ProveedorConProductos p)
                AbrirFicha(p);
        };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || grid.CurrentRow?.Tag is not ProveedorConProductos p) return;
            e.Handled = true;   // si no, Enter baja a la fila siguiente
            AbrirFicha(p);
        };

        grid.ColumnHeadersHeight = 45;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        EstiloProveedores.TablaMarcada(grid);

        // Cada columna tiene su ancho mínimo; si sobra sitio, se reparte entre todas (si se lo queda
        // solo el nombre, que es corto, queda un hueco grande hasta el tipo)
        (string Nombre, int Ancho)[] columnas =
        {
            ("Nº", 70), ("NOMBRE", 380), ("FECHA ALTA", 130), ("ESTADO", 105), ("TIPO", 175),
            ("IMPACTO", 95), ("ESTATUS", 95), ("RECLAMACIONES", 140), ("EVALUACIÓN", 120)
        };
        foreach (var (nombre, ancho) in columnas)
        {
            int i = grid.Columns.Add(nombre, nombre);
            grid.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            grid.Columns[i].MinimumWidth = ancho;
            grid.Columns[i].FillWeight = ancho;
        }
        // Al nombre le toca algo más del sitio que sobra: hay nombres largos (INCOLTEC - INNOVATION COLOUER...)
        grid.Columns[ColumnaNombre].FillWeight = 700;

        grid.Columns[ColumnaNumero].DefaultCellStyle.ForeColor = Tema.Acento;
        grid.Columns[ColumnaNumero].DefaultCellStyle.Font = Tema.FuenteNegrita;

        return grid;
    }

    // Estado y evaluación se dibujan como etiquetas de color
    private void PintarEtiquetas(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Graphics == null) return;
        if (grid.Rows[e.RowIndex].Tag is not ProveedorConProductos p) return;

        if (e.ColumnIndex == ColumnaEstado)
        {
            var (fondo, color) = p.Proveedor.Baja
                ? Tema.ColorNotaProveedor("")   // gris
                : Tema.ColorNotaProveedor("A"); // verde
            Componentes.PintarEtiqueta(e, p.Proveedor.Baja ? "Baja" : "Activo", fondo, color);
        }
        else if (e.ColumnIndex == ColumnaEvaluacion && ProveedorServicio.Evaluacion(p.Proveedor) is string nota)
        {
            var (fondo, color) = Tema.ColorNotaProveedor(nota);
            Componentes.PintarEtiqueta(e, nota, fondo, color);
        }
    }

    // Nº, fecha y notas se ordenan por el dato de verdad, no por cómo se ve; los vacíos al final
    private void CompararParaOrdenar(object? sender, DataGridViewSortCompareEventArgs e)
    {
        if (grid.Rows[e.RowIndex1].Tag is not ProveedorConProductos a || grid.Rows[e.RowIndex2].Tag is not ProveedorConProductos b)
            return;

        static int Vacios<T>(T? x, T? y) where T : struct, IComparable<T> =>
            x == null ? (y == null ? 0 : 1) : y == null ? -1 : x.Value.CompareTo(y.Value);

        var pa = a.Proveedor;
        var pb = b.Proveedor;
        int? resultado = e.Column.Index switch
        {
            ColumnaNumero => Vacios(pa.Numero, pb.Numero),
            ColumnaAlta => Vacios(pa.FechaAlta, pb.FechaAlta),
            ColumnaImpacto => Vacios(pa.Impacto, pb.Impacto),
            ColumnaEstatus => Vacios(pa.Estatus, pb.Estatus),
            ColumnaReclamaciones => Vacios(pa.Reclamaciones, pb.Reclamaciones),
            _ => null
        };
        if (resultado == null) return;

        e.SortResult = resultado.Value != 0 ? resultado.Value : string.Compare(pa.Nombre, pb.Nombre, StringComparison.CurrentCultureIgnoreCase);
        e.Handled = true;
    }

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
        static string Nota(byte? n) => n?.ToString() ?? "—";

        // Los que en el Excel tenían datos distintos según la fila llevan ⚠ delante del nombre
        fila.CreateCells(grid,
            prov.Numero?.ToString() ?? "—",
            (prov.Revisar != null ? "⚠  " : "") + prov.Nombre,
            prov.FechaAlta?.ToString("dd/MM/yy") ?? "—",
            prov.Baja ? "Baja" : "Activo",
            prov.Tipo ?? "—",
            Nota(prov.Impacto),
            Nota(prov.Estatus),
            Nota(prov.Reclamaciones),
            ProveedorServicio.Evaluacion(prov) ?? "—");

        if (prov.Baja)
            fila.DefaultCellStyle.ForeColor = Tema.Suave;

        string? evaluacion = ProveedorServicio.Evaluacion(prov);
        fila.Cells[ColumnaEvaluacion].ToolTipText = evaluacion == null
            ? "Falta el impacto, el estatus o las reclamaciones"
            : $"Impacto + estatus + reclamaciones = {(prov.Impacto ?? 0) + (prov.Estatus ?? 0) + (prov.Reclamaciones ?? 0)}"
              + $"\n{ProveedorServicio.QueSupone(evaluacion)}";
        if (prov.Revisar != null)
            fila.Cells[ColumnaNombre].ToolTipText = "En el Excel había datos distintos según la fila:\n" + prov.Revisar;

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
                && estado switch
                {
                    EstadoActivos => !p.Proveedor.Baja,
                    EstadoBaja => p.Proveedor.Baja,
                    _ => true
                })
            .ToList();
    }

    // De momento la ficha solo es de consulta: no hace falta recargar la lista al cerrarla
    private void AbrirFicha(ProveedorConProductos p)
    {
        using var ficha = new FormProveedor(p);
        ficha.ShowDialog(this);
    }

    private void LimpiarFiltros()
    {
        // Cada control que se limpia lanzaría AplicarFiltros: se rehace la tabla una sola vez al final
        limpiando = true;
        txtBuscar.Clear();
        cmbTipo.SelectedIndex = 0;
        cmbEvaluacion.SelectedIndex = 0;
        cmbEstado.SelectedItem = EstadoActivos;
        limpiando = false;
        AplicarFiltros();
    }

    private void ActualizarContador(List<ProveedorConProductos> visibles)
    {
        int revisar = visibles.Count(p => p.Proveedor.Revisar != null);
        lblContador.Text = $"{visibles.Count} {(visibles.Count == 1 ? "proveedor" : "proveedores")}"
            + (revisar > 0 ? $"  ·  ⚠ {revisar} con datos distintos en el Excel" : "");

        lblVacio.Text = registros.Count == 0
            ? "Todavía no hay proveedores. Se pasan del Excel con BaseDatos\\importar_proveedores.ps1."
            : "No hay proveedores que coincidan con los filtros.";
        lblVacio.Visible = visibles.Count == 0;
        grid.Visible = visibles.Count > 0;
    }
}
