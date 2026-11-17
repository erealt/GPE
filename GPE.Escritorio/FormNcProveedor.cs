using System.Data.Common;
using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Las NC, incidencias y mejoras enlazadas a un proveedor, desde el botón "Ver NC" bajo Reclamaciones en su ficha.
// Doble clic (o Enter) abre el registro; si se guarda, la lista se vuelve a leer.
public class FormNcProveedor : Form
{
    private const int ColumnaTipo = 2;
    private const int ColumnaEstado = 5;

    private readonly NoConformidadServicio servicio = new();
    private readonly int proveedorId;
    private readonly DataGridView grid = new();
    private readonly Label lblSubtitulo = new();

    public List<NoConformidad> Registros { get; private set; }

    // Si se ha guardado algún registro: la ficha del proveedor vuelve a contar
    public bool HayCambios { get; private set; }

    public FormNcProveedor(Proveedor proveedor, List<NoConformidad> registros)
    {
        proveedorId = proveedor.Id;
        Registros = registros;

        Text = $"No conformidades - {proveedor.Nombre}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(900, 500);
        Componentes.AjustarAPantalla(this, 0.75);
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var (scroll, pila) = Componentes.Pagina();
        pila.Controls.Add(Componentes.Cabecera(
            $"PROVEEDORES  ·  Nº {proveedor.Numero?.ToString() ?? CamposProveedor.SinNota}  ·  {proveedor.Nombre}",
            "No conformidades", "Las que tienen elegido este proveedor."));

        var tarjeta = Componentes.Tarjeta("Registros", Subtitulo(), null, lblSubtitulo);
        ConfigurarGrid();
        Mostrar();
        tarjeta.Controls.Add(grid);
        pila.Controls.Add(CamposProveedor.Marcada(tarjeta, EstiloProveedores.RayaEvaluacion));

        var btnCerrar = Tema.BotonSecundario("Cerrar", 140);
        btnCerrar.Click += (s, e) => Close();
        pila.Controls.Add(Componentes.Acciones(btnCerrar));
        Controls.Add(scroll);

        Shown += (s, e) => grid.ClearSelection();
    }

    private string Subtitulo()
    {
        int ultimoAno = NoConformidadServicio.DelUltimoAno(Registros);
        return Registros.Count == 0
            ? "Este proveedor no tiene ninguna."
            : $"{ultimoAno} en los últimos 12 meses y {Registros.Count} en total. Doble clic en una para abrirla.";
    }

    private void ConfigurarGrid()
    {
        Componentes.EstiloTabla(grid, altoFila: 44);
        grid.Dock = DockStyle.Top;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ShowCellToolTips = true;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.CellPainting += PintarEtiquetas;
        EstiloProveedores.TablaMarcada(grid);

        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is NoConformidad nc)
                Abrir(nc);
        };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || grid.CurrentRow?.Tag is not NoConformidad nc) return;
            e.Handled = true;
            Abrir(nc);
        };

        (string Nombre, int Ancho)[] columnas =
        {
            ("Nº", 90), ("FECHA", 100), ("TIPO", 130), ("ORIGEN", 120), ("PROBLEMA", 400), ("ESTADO", 140)
        };
        foreach (var (nombre, ancho) in columnas)
        {
            int i = grid.Columns.Add(nombre, nombre);
            grid.Columns[i].MinimumWidth = ancho;
            grid.Columns[i].FillWeight = ancho;
            grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        grid.Columns[4].FillWeight = 800;
    }

    private void Mostrar()
    {
        grid.Rows.Clear();
        foreach (var nc in Registros)
        {
            var fila = new DataGridViewRow { Tag = nc, Height = grid.RowTemplate.Height };
            string problema = nc.Problema.ReplaceLineEndings(" ");
            fila.CreateCells(grid,
                nc.Numero ?? CamposProveedor.SinNota,
                nc.FechaDetectado.ToString("dd/MM/yy"),
                nc.TipoRegistro,
                nc.Origen ?? CamposProveedor.SinNota,
                problema,
                NoConformidadServicio.Estado(nc));
            fila.Cells[4].ToolTipText = nc.Problema.Length > 600 ? nc.Problema[..600] + "…" : nc.Problema;
            grid.Rows.Add(fila);
        }
        Componentes.AjustarAltoTabla(grid);
        grid.ClearSelection();
    }

    // Tipo y estado como etiquetas de color, igual que en la lista de calidad
    private void PintarEtiquetas(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Graphics == null) return;
        if (grid.Rows[e.RowIndex].Tag is not NoConformidad nc) return;

        if (e.ColumnIndex == ColumnaTipo)
        {
            var (fondo, texto) = Tema.ColorTipoRegistro(nc.TipoRegistro);
            Componentes.PintarEtiqueta(e, nc.TipoRegistro, fondo, texto);
        }
        else if (e.ColumnIndex == ColumnaEstado)
        {
            string estado = NoConformidadServicio.Estado(nc);
            var (fondo, texto) = Tema.ColorEstadoRegistro(estado);
            Componentes.PintarEtiqueta(e, estado, fondo, texto);
        }
    }

    // Como en la lista de calidad: se vuelve a leer por si otro lo ha cambiado
    private void Abrir(NoConformidad nc)
    {
        NoConformidad? actual;
        try
        {
            actual = servicio.ObtenerRegistro(nc.Id);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo cargar el registro.", ex);
            return;
        }
        if (actual == null)
        {
            Mensajes.Informacion(this, "Registro no encontrado", "Este registro ya no existe: puede que otro usuario lo haya borrado.");
            Recargar();
            return;
        }

        using (var ficha = new FormNoConformidad(actual))
            if (ficha.ShowDialog(this) != DialogResult.OK) return;

        HayCambios = true;
        Recargar();
    }

    private void Recargar()
    {
        try
        {
            Registros = servicio.ObtenerDeProveedor(proveedorId);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo volver a leer la lista.", ex);
            return;
        }
        lblSubtitulo.Text = Subtitulo();
        Mostrar();
    }
}
