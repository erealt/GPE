using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Globalization;

namespace GPE.Escritorio;

public class FormHistorial : Form
{
    private static readonly Color ColorFondo = Tema.Fondo;
    private static readonly Color ColorTexto = Tema.Texto;
    private static readonly Color ColorSuave = Tema.Suave;

    private static readonly CultureInfo Es = new("es-ES");

    private readonly DevolucionServicio servicio = new();
    private readonly DevolucionFila devolucion;

    private readonly DataGridView grid = new();

    public FormHistorial(DevolucionFila devolucion)
    {
        this.devolucion = devolucion;

        Text = $"Historial · {devolucion.Numero}";
        Size = new Size(1100, 600);
        MinimumSize = new Size(800, 400);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ColorFondo;
        Font = new Font("Segoe UI", 10);

        var titulo = new Label
        {
            Text = "Historial de cambios",
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            ForeColor = ColorTexto,
            Dock = DockStyle.Top,
            Height = 50
        };

        var subtitulo = new Label
        {
            Text = $"Quién ha modificado qué en la devolución {devolucion.Numero} de {devolucion.Cliente}.",
            ForeColor = ColorSuave,
            Dock = DockStyle.Top,
            Height = 35
        };

        var contenido = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30, 20, 30, 20) };
        contenido.Controls.Add(ConfigurarGrid());
        contenido.Controls.Add(subtitulo);
        contenido.Controls.Add(titulo);
        Controls.Add(contenido);

        Load += (s, e) => CargarHistorial();
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
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCells;

        grid.EnableHeadersVisualStyles = false;
        grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
        grid.ColumnHeadersHeight = 40;
        grid.ColumnHeadersDefaultCellStyle.BackColor = Tema.CabeceraTabla;
        grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Tema.CabeceraTabla;
        grid.ColumnHeadersDefaultCellStyle.ForeColor = ColorSuave;
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);

        grid.DefaultCellStyle.ForeColor = ColorTexto;
        grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;
        grid.DefaultCellStyle.SelectionForeColor = ColorTexto;
        grid.DefaultCellStyle.Padding = new Padding(6, 6, 6, 6);
        grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True;

        grid.Columns.Add("FECHA", "FECHA");
        grid.Columns.Add("USUARIO", "USUARIO");
        grid.Columns.Add("ACCION", "ACCIÓN");
        grid.Columns.Add("ELEMENTO", "ELEMENTO");
        grid.Columns.Add("DETALLE", "DETALLE");

        grid.Columns[0].FillWeight = 16;
        grid.Columns[1].FillWeight = 16;
        grid.Columns[2].FillWeight = 12;
        grid.Columns[3].FillWeight = 18;
        grid.Columns[4].FillWeight = 50;

        return grid;
    }

    private void CargarHistorial()
    {
        List<EntradaHistorial> entradas;
        try
        {
            entradas = servicio.ObtenerHistorial(devolucion.Id);
        }
        catch (DbException ex)
        {
            MessageBox.Show(this,
                "No se pudo cargar el historial de la base de datos.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        foreach (var e in entradas)
            grid.Rows.Add(e.Fecha.ToString("dd MMM yyyy HH:mm", Es), e.Usuario, e.Accion, e.Elemento, e.Detalle);

        if (entradas.Count == 0)
            grid.Rows.Add("—", "—", "—", "—", "Todavía no hay cambios registrados.");

        grid.ClearSelection();
    }
}
