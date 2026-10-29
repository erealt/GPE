using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Qué puntos se revisan en cada máquina o sección: las casillas que en la hoja estaban
// en gris. Se pulsa una casilla para quitarla o ponerla, y una cabecera (o el nombre
// del punto) para toda la columna (o la fila).
public class FormPlantillaRutina : Form
{
    private readonly RutinaServicio servicio = new();
    private PlantillaRutina plantilla = new(new(), new(), new());
    private readonly HashSet<(int PuntoId, int SeccionId)> aplica = new();
    private readonly DataGridView grid = new();
    private bool hayCambios;

    public FormPlantillaRutina()
    {
        Text = "Plantilla de la rutina de calidad";
        Size = new Size(1300, 800);
        MinimumSize = new Size(900, 500);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(30, 20, 30, 20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Pulsa una casilla para indicar si ese punto se revisa en esa máquina. " +
                   "Pulsa la cabecera de una máquina o el nombre de un punto para cambiar toda la columna o la fila.\n" +
                   "Lo ya marcado en rondas anteriores no se pierde. Para añadir máquinas o puntos, de momento hay que hacerlo en la base de datos.",
            AutoSize = true,
            MaximumSize = new Size(1200, 0),
            ForeColor = Tema.Suave,
            Margin = new Padding(0, 0, 0, 12)
        }, 0, 0);
        layout.Controls.Add(ConfigurarGrid(), 0, 1);

        var btnGuardar = Tema.BotonPrincipal("Guardar plantilla", 180);
        btnGuardar.Click += (s, e) => Guardar();
        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCancelar;
        var acciones = Componentes.Acciones(btnGuardar, btnCancelar);
        acciones.Margin = new Padding(0, 15, 0, 0);
        layout.Controls.Add(acciones, 0, 2);

        Controls.Add(layout);
        Load += (s, e) => Cargar();
        FormClosing += (s, e) =>
        {
            if (DialogResult != DialogResult.OK && hayCambios
                && !Mensajes.DescartarCambios(this, "Hay cambios sin guardar. ¿Salir sin guardarlos?"))
                e.Cancel = true;
        };
    }

    private Control ConfigurarGrid()
    {
        Componentes.EstiloTabla(grid, altoFila: 30);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.GridColor = Tema.LineaTabla;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.AllowUserToResizeColumns = false;
        grid.MultiSelect = false;
        grid.ColumnHeadersHeight = 76;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        grid.CellPainting += Pintar;
        grid.CellClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            var punto = (RutinaPunto)grid.Rows[e.RowIndex].Tag!;
            var secciones = e.ColumnIndex == 0
                ? plantilla.Secciones
                : new List<RutinaSeccion> { (RutinaSeccion)grid.Columns[e.ColumnIndex].Tag! };
            Cambiar(secciones.Select(sec => (punto.Id, sec.Id)));
        };
        grid.ColumnHeaderMouseClick += (s, e) =>
        {
            if (e.ColumnIndex <= 0) return;
            var seccion = (RutinaSeccion)grid.Columns[e.ColumnIndex].Tag!;
            Cambiar(plantilla.Puntos.Select(p => (p.Id, seccion.Id)));
        };
        return grid;
    }

    // Si todas estaban puestas, las quita; si no, las pone todas
    private void Cambiar(IEnumerable<(int, int)> casillas)
    {
        var lista = casillas.ToList();
        bool quitar = lista.All(aplica.Contains);
        foreach (var casilla in lista)
            if (quitar) aplica.Remove(casilla); else aplica.Add(casilla);
        hayCambios = true;
        grid.Invalidate();
    }

    private void Cargar()
    {
        try
        {
            plantilla = servicio.ObtenerPlantilla();
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo cargar la plantilla.", ex);
            return;
        }
        aplica.UnionWith(plantilla.Aplica);

        var columnaPunto = new DataGridViewTextBoxColumn
        {
            HeaderText = "PUNTO DE CONTROL",
            Width = 220,
            Frozen = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        columnaPunto.DefaultCellStyle.Padding = new Padding(10, 0, 4, 0);
        columnaPunto.DefaultCellStyle.BackColor = Tema.CabeceraTabla;
        columnaPunto.DefaultCellStyle.SelectionBackColor = Tema.CabeceraTabla;
        columnaPunto.DefaultCellStyle.Font = Tema.FuentePequenaNegrita;
        grid.Columns.Add(columnaPunto);

        foreach (var seccion in plantilla.Secciones)
        {
            var columna = new DataGridViewTextBoxColumn
            {
                HeaderText = seccion.Nombre,
                Width = ModuloRutinaCalidad.AnchoColumna(seccion.Nombre),
                Tag = seccion,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            columna.HeaderCell.ToolTipText = Planta.Nombre(seccion.Planta);
            grid.Columns.Add(columna);
        }

        foreach (var punto in plantilla.Puntos)
        {
            int i = grid.Rows.Add(punto.Nombre);
            grid.Rows[i].Tag = punto;
        }
        grid.ClearSelection();
    }

    private void Pintar(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics == null || e.RowIndex < 0 || e.ColumnIndex < 1) return;
        var punto = (RutinaPunto)grid.Rows[e.RowIndex].Tag!;
        var seccion = (RutinaSeccion)grid.Columns[e.ColumnIndex].Tag!;
        bool revisa = aplica.Contains((punto.Id, seccion.Id));

        var r = e.CellBounds;
        using (var relleno = new SolidBrush(revisa ? Color.White : Tema.FondoNoRevisa))
            e.Graphics.FillRectangle(relleno, r);
        using (var borde = new Pen(Tema.LineaTabla))
        {
            e.Graphics.DrawLine(borde, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
            e.Graphics.DrawLine(borde, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
        }
        if (revisa)
            TextRenderer.DrawText(e.Graphics, "✓", Tema.FuenteNegrita, r, Tema.Acento,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        e.Handled = true;
    }

    private void Guardar()
    {
        // Las de puntos o secciones desactivados no salen en la tabla: se quedan como estaban
        var casillas = aplica.Concat(plantilla.Aplica.Where(a =>
                !plantilla.Puntos.Any(p => p.Id == a.PuntoId) || !plantilla.Secciones.Any(s => s.Id == a.SeccionId)))
            .ToHashSet();
        try
        {
            servicio.GuardarPlantilla(casillas);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar la plantilla.", ex);
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
