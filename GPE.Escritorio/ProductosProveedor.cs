using System.Data.Common;
using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Pestaña Productos de la ficha del proveedor: tabla con filtro Activos / De baja / Todos y botón para añadir.
// Cada producto se añade y se cambia en su propia ventana (FormProductoProveedor), que guarda al momento.
public class ProductosProveedor
{
    private const int ColumnaEstado = 3;
    private const int ColumnaMuestreo = 4;
    private const int ColumnaVulnerabilidad = 5;
    private const int ColumnaRiesgo = 6;
    private const int ColumnaActuacion = 7;

    private readonly ProveedorServicio servicio = new();
    private readonly int proveedorId;
    private readonly Func<Proveedor> proveedorActual;   // lo que hay escrito en la ficha, para las notas
    private readonly DataGridView grid = new();
    private readonly ComboBox cmbEstado = new() { DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly Label lblSubtitulo = new();

    public List<ProductoProveedor> Productos { get; private set; }
    public TableLayoutPanel Tarjeta { get; }

    // El filtro de la tabla no es un dato del proveedor: no cuenta como cambio sin guardar
    public Control Filtro => cmbEstado;

    // Después de guardar un producto, con la lista ya leída de nuevo
    public event EventHandler? Cambiados;

    public ProductosProveedor(int proveedorId, List<ProductoProveedor> productos, Func<Proveedor> proveedorActual)
    {
        this.proveedorId = proveedorId;
        this.proveedorActual = proveedorActual;
        Productos = productos;
        // Un producto necesita el proveedor guardado
        Tarjeta = proveedorId == 0
            ? Componentes.Tarjeta("Productos", "Guarda el proveedor: se abrirá aquí para añadirle sus productos.")
            : CrearTarjeta();
    }

    // Las notas de los productos dependen de las del proveedor: se vuelven a pintar si cambian
    public void Repintar() => grid.Invalidate();

    public void QuitarSeleccion() => grid.ClearSelection();

    private TableLayoutPanel CrearTarjeta()
    {
        // Como el filtro del Excel: al abrir se ven solo los productos activos
        cmbEstado.Width = 140;
        cmbEstado.Margin = new Padding(0, 3, 10, 0);
        cmbEstado.Items.AddRange(new object[] { CamposProveedor.FiltroActivos, CamposProveedor.FiltroBaja, CamposProveedor.FiltroTodos });
        cmbEstado.SelectedItem = Productos.Count(x => !x.Baja) > 0 || Productos.Count == 0
            ? CamposProveedor.FiltroActivos
            : CamposProveedor.FiltroTodos;
        cmbEstado.SelectedIndexChanged += (s, e) => Mostrar();

        var btnAnadir = Tema.BotonPrincipal("+  Añadir producto", 190);
        btnAnadir.Margin = Padding.Empty;
        btnAnadir.Click += (s, e) => Abrir(null);

        var acciones = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        acciones.Controls.Add(cmbEstado);
        acciones.Controls.Add(btnAnadir);
        var tarjeta = Componentes.Tarjeta("Productos", Subtitulo(), acciones, lblSubtitulo);

        ConfigurarGrid();
        Mostrar();
        tarjeta.Controls.Add(grid);
        return tarjeta;
    }

    private string Subtitulo()
    {
        int activos = Productos.Count(x => !x.Baja);
        int deBaja = Productos.Count - activos;
        return Productos.Count == 0
            ? "Este proveedor todavía no tiene productos."
            : $"{activos} {(activos == 1 ? "activo" : "activos")}" + (deBaja > 0 ? $" y {deBaja} de baja" : "")
              + ". El muestreo y el riesgo de fraude son de cada producto. Doble clic en uno para cambiarlo.";
    }

    private void ConfigurarGrid()
    {
        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is ProductoProveedor x)
                Abrir(x);
        };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || grid.CurrentRow?.Tag is not ProductoProveedor x) return;
            e.Handled = true;   // si no, Enter baja a la fila siguiente
            Abrir(x);
        };

        Componentes.EstiloTabla(grid, altoFila: 44);
        grid.Dock = DockStyle.Top;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ShowCellToolTips = true;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.CellPainting += PintarEtiquetas;
        grid.CellFormatting += TextoCalculado;
        EstiloProveedores.TablaMarcada(grid);

        (string Nombre, int Ancho)[] columnas =
        {
            ("CÓDIGO", 130), ("DESCRIPCIÓN", 320), ("TIPO PRODUCTO", 160), ("ESTADO", 100),
            ("MUESTREO", 110), ("RIESGO", 110), ("RIESGO FRAUDE", 140), ("ACTUACIÓN", 230)
        };
        foreach (var (nombre, ancho) in columnas)
        {
            int i = grid.Columns.Add(nombre, nombre);
            grid.Columns[i].MinimumWidth = ancho;
            grid.Columns[i].FillWeight = ancho;
            grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        grid.Columns[1].FillWeight = 600;
    }

    // x == null: producto nuevo. Las notas se calculan con lo que hay escrito en la ficha del proveedor.
    private void Abrir(ProductoProveedor? x)
    {
        var ventana = grid.FindForm();
        using var ficha = new FormProductoProveedor(proveedorActual(), x);
        if (ficha.ShowDialog(ventana) != DialogResult.OK) return;

        try
        {
            Productos = servicio.ObtenerProductos(proveedorId);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(ventana!, "El producto se ha guardado, pero no se pudo volver a leer la lista de productos.", ex);
            return;
        }
        lblSubtitulo.Text = Subtitulo();
        Mostrar();
        Cambiados?.Invoke(this, EventArgs.Empty);
    }

    private void Mostrar()
    {
        string estado = cmbEstado.SelectedItem as string ?? CamposProveedor.FiltroTodos;
        grid.Rows.Clear();
        // Con "Todos", primero los activos y los de baja al final
        foreach (var x in Productos.OrderBy(x => x.Baja))
            if (estado == CamposProveedor.FiltroTodos || x.Baja == (estado == CamposProveedor.FiltroBaja))
                grid.Rows.Add(CrearFila(x));

        Componentes.AjustarAltoTabla(grid);
        grid.ClearSelection();
    }

    private DataGridViewRow CrearFila(ProductoProveedor x)
    {
        // Las filas creadas a mano no cogen el alto de RowTemplate, y AjustarAltoTabla cuenta con él
        var fila = new DataGridViewRow { Tag = x, Height = grid.RowTemplate.Height };
        fila.CreateCells(grid,
            x.Codigo ?? CamposProveedor.SinNota,
            x.Descripcion ?? CamposProveedor.SinNota,
            x.TipoProducto ?? CamposProveedor.SinNota,
            x.Baja ? CamposProveedor.Baja : CamposProveedor.Activo,
            "", "", "", "");   // muestreo, riesgo, riesgo fraude y actuación dependen de las notas del proveedor: TextoCalculado

        if (x.Baja)
            fila.DefaultCellStyle.ForeColor = Tema.Suave;
        return fila;
    }

    // Muestreo, riesgo y actuación se calculan al pintar, con las notas del proveedor que hay escritas
    private void TextoCalculado(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || grid.Rows[e.RowIndex].Tag is not ProductoProveedor x) return;
        var p = proveedorActual();
        var celda = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];

        switch (e.ColumnIndex)
        {
            case ColumnaMuestreo:
                string? muestreo = ProveedorServicio.FrecuenciaMuestreo(p, x);
                e.Value = muestreo ?? CamposProveedor.SinNota;
                celda.ToolTipText = muestreo == null
                    ? "Falta el impacto, el estatus o el control en recepción"
                    : $"Impacto {Nota(ProveedorServicio.ImpactoProducto(p, x))} + estatus {Nota(p.Estatus)}"
                      + $" + control recepción {Nota(x.ControlRecepcion)} + reclamaciones {Nota(x.Reclamaciones)}"
                      + $"\n{ProveedorServicio.AlcanceMuestreo(muestreo)}";
                break;
            case ColumnaVulnerabilidad:
                string? vulnerabilidad = ProveedorServicio.RiesgoVulnerabilidad(x);
                e.Value = vulnerabilidad ?? CamposProveedor.SinNota;
                celda.ToolTipText = vulnerabilidad == null
                    ? "Faltan las notas de fraude del producto"
                    : $"O × D = {ProveedorServicio.OcurrenciaPorDeteccion(x)}";
                break;
            case ColumnaRiesgo:
                string? riesgo = ProveedorServicio.RiesgoFraude(p, x);
                e.Value = riesgo ?? CamposProveedor.SinNota;
                celda.ToolTipText = riesgo == null
                    ? "Faltan las notas de fraude del producto"
                    : $"Puntuación = {ProveedorServicio.Puntuacion(p, x)}";
                break;
            case ColumnaActuacion:
                e.Value = ProveedorServicio.Actuacion(ProveedorServicio.RiesgoFraude(p, x)) ?? CamposProveedor.SinNota;
                break;
        }
    }

    // Estado, muestreo y riesgo se dibujan como etiquetas de color
    private void PintarEtiquetas(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Graphics == null) return;
        if (grid.Rows[e.RowIndex].Tag is not ProductoProveedor x) return;

        var p = proveedorActual();
        string? texto = e.ColumnIndex switch
        {
            ColumnaEstado => x.Baja ? CamposProveedor.Baja : CamposProveedor.Activo,
            ColumnaMuestreo => ProveedorServicio.FrecuenciaMuestreo(p, x),
            ColumnaVulnerabilidad => ProveedorServicio.RiesgoVulnerabilidad(x),
            ColumnaRiesgo => ProveedorServicio.RiesgoFraude(p, x),
            _ => null
        };
        if (texto == null) return;

        // Estado: verde si está activo y gris si es de baja
        var (fondo, color) = e.ColumnIndex == ColumnaEstado
            ? Tema.ColorNotaProveedor(x.Baja ? "" : "A")
            : Tema.ColorNotaProveedor(texto);
        Componentes.PintarEtiqueta(e, texto, fondo, color);
    }

    private static string Nota(byte? nota) => nota?.ToString() ?? CamposProveedor.SinNota;
}
