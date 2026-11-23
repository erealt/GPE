using System.Data.Common;
using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Tarjeta de los certificados (columna CERTIFICADOS del Excel) en la ficha del proveedor: cada certificado con su fecha de caducidad y una etiqueta
// verde (vigente), naranja (caduca en 30 días o menos) o roja (caducado). Se añaden y se cambian en
// FormCertificadoProveedor, que guarda al momento.
public class CertificadosProveedor
{
    private const int ColumnaEstado = 2;
    // En el Excel, CERTIFICADOS va en el segundo apartado "DATOS SOBRE PROVEEDOR" (columnas K-N).
    // FormProveedor añade debajo ensayo trazabilidad, auditoría y RFA.
    private const string Titulo = "Datos sobre proveedor";

    private readonly ProveedorServicio servicio = new();
    private readonly Proveedor proveedor;
    private readonly DataGridView grid = new();
    private readonly Label lblSubtitulo = new();
    private List<CertificadoProveedor> certificados = new();

    public TableLayoutPanel Tarjeta { get; }

    public CertificadosProveedor(Proveedor proveedor)
    {
        this.proveedor = proveedor;
        // Un certificado necesita el proveedor guardado
        Tarjeta = proveedor.Id == 0
            ? Componentes.Tarjeta(Titulo, "Guarda el proveedor para poder añadirle sus certificados.")
            : CrearTarjeta();
    }

    public void QuitarSeleccion() => grid.ClearSelection();

    private TableLayoutPanel CrearTarjeta()
    {
        var btnAnadir = Tema.BotonPrincipal("+  Añadir certificado", 210);
        btnAnadir.Margin = Padding.Empty;
        btnAnadir.Click += (s, e) => Abrir(null);
        var tarjeta = Componentes.Tarjeta(Titulo, "", btnAnadir, lblSubtitulo);

        ConfigurarGrid();
        try
        {
            certificados = servicio.ObtenerCertificados(proveedor.Id);
        }
        catch (DbException)
        {
            lblSubtitulo.Text = "No se pudieron leer los certificados.";
            btnAnadir.Enabled = false;
            grid.Visible = false;
            tarjeta.Controls.Add(grid);
            return tarjeta;
        }
        Mostrar();
        tarjeta.Controls.Add(grid);
        return tarjeta;
    }

    private void ConfigurarGrid()
    {
        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is CertificadoProveedor c)
                Abrir(c);
        };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || grid.CurrentRow?.Tag is not CertificadoProveedor c) return;
            e.Handled = true;
            Abrir(c);
        };

        Componentes.EstiloTabla(grid, altoFila: 44);
        grid.Dock = DockStyle.Top;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.CellPainting += PintarEstado;
        EstiloProveedores.TablaMarcada(grid);

        (string Nombre, int Ancho)[] columnas = { ("CERTIFICADO", 260), ("CADUCA EL", 140), ("ESTADO", 200) };
        foreach (var (nombre, ancho) in columnas)
        {
            int i = grid.Columns.Add(nombre, nombre);
            grid.Columns[i].MinimumWidth = ancho;
            grid.Columns[i].FillWeight = ancho;
            grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
        }
    }

    // c == null: certificado nuevo
    private void Abrir(CertificadoProveedor? c)
    {
        var ventana = grid.FindForm();
        using (var ficha = new FormCertificadoProveedor(proveedor, c))
            if (ficha.ShowDialog(ventana) != DialogResult.OK) return;

        try
        {
            certificados = servicio.ObtenerCertificados(proveedor.Id);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(ventana!, "El certificado se ha guardado, pero no se pudo volver a leer la lista.", ex);
            return;
        }
        Mostrar();
    }

    private void Mostrar()
    {
        grid.Rows.Clear();
        foreach (var c in certificados)
        {
            var fila = new DataGridViewRow { Tag = c, Height = grid.RowTemplate.Height };
            fila.CreateCells(grid, c.Nombre, c.Caduca.ToString("dd/MM/yyyy", Componentes.Es), ProveedorServicio.TextoCaducidad(c));
            grid.Rows.Add(fila);
        }
        grid.Visible = certificados.Count > 0;
        Componentes.AjustarAltoTabla(grid);
        grid.ClearSelection();
        lblSubtitulo.Text = Subtitulo();
    }

    private string Subtitulo()
    {
        if (certificados.Count == 0)
            return "Todavía no tiene certificados con fecha. Añádelos para que GPE avise antes de que caduquen.";

        int caducados = certificados.Count(c => ProveedorServicio.Estado(c) == ProveedorServicio.EstadoCertificado.Caducado);
        int porCaducar = certificados.Count(c => ProveedorServicio.Estado(c) == ProveedorServicio.EstadoCertificado.PorCaducar);
        var partes = new List<string>();
        if (caducados > 0) partes.Add(caducados == 1 ? "1 caducado" : $"{caducados} caducados");
        if (porCaducar > 0) partes.Add(porCaducar == 1 ? "1 caduca pronto" : $"{porCaducar} caducan pronto");
        string estado = partes.Count == 0 ? "Todos vigentes" : "⚠  " + string.Join(" y ", partes);
        return $"{estado}. Doble clic en uno para cambiar la fecha o quitarlo.";
    }

    // Estado: verde vigente, naranja caduca en 30 días o menos, rojo caducado
    private void PintarEstado(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != ColumnaEstado || e.Graphics == null) return;
        if (grid.Rows[e.RowIndex].Tag is not CertificadoProveedor c) return;

        var (fondo, color) = Tema.ColorNotaProveedor(ProveedorServicio.Estado(c) switch
        {
            ProveedorServicio.EstadoCertificado.Caducado => "C",
            ProveedorServicio.EstadoCertificado.PorCaducar => "B",
            _ => "A"
        });
        Componentes.PintarEtiqueta(e, ProveedorServicio.TextoCaducidad(c), fondo, color);
    }
}
