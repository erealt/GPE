using GPE.Datos;
using GPE.Logica;
using System.Globalization;

namespace GPE.Escritorio;

// Acciones o seguimientos de la ficha de calidad. Los seguimientos no tienen "hecha" y la fecha es obligatoria.
public class TablaAcciones : TableLayoutPanel
{
    private const int ColHecha = 0, ColAccion = 1, ColFecha = 2, ColResponsable = 3, ColObservaciones = 4, ColQuitar = 5;
    private static readonly string[] FormatosFecha = { "d/M/yyyy", "d/M/yy", "d-M-yyyy", "d-M-yy" };

    private readonly string clase;
    private bool EsSeguimiento => clase == ClaseAccion.Seguimiento;
    private readonly DataGridView grid = new();
    private readonly DataGridViewComboBoxColumn colResponsable = new();
    private readonly Label lblVacia = new();
    private readonly Label lblError = CamposFormulario.EtiquetaError();
    private readonly Button btnAnadir;
    private bool cargando;

    // El usuario ha cambiado algo (para el aviso de cambios sin guardar)
    public event EventHandler? Cambiado;

    public TablaAcciones(string clase, IEnumerable<string> personas, string textoVacia)
    {
        this.clase = clase;

        Dock = DockStyle.Fill;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        ColumnCount = 1;
        Margin = new Padding(0);
        ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        ConfigurarGrid(personas);

        lblVacia.Text = textoVacia;
        lblVacia.AutoSize = true;
        lblVacia.ForeColor = Tema.Suave;
        lblVacia.Margin = new Padding(6, 10, 0, 0);

        btnAnadir = Tema.BotonSecundario(EsSeguimiento ? "+ Añadir seguimiento" : "+ Añadir acción", 210);
        btnAnadir.Margin = new Padding(0, 10, 0, 0);
        btnAnadir.Click += (s, e) => AnadirFila();

        Controls.Add(grid);
        Controls.Add(lblVacia);
        Controls.Add(lblError);
        Controls.Add(btnAnadir);
        AjustarAlto();
    }

    // Solo consulta: no se puede escribir, añadir ni quitar filas
    [System.ComponentModel.Browsable(false)]
    [System.ComponentModel.DesignerSerializationVisibility(
        System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool SoloLectura
    {
        get => grid.ReadOnly;
        set
        {
            grid.ReadOnly = value;
            grid.Columns[ColQuitar].Visible = !value;
            btnAnadir.Visible = !value;
        }
    }

    private void ConfigurarGrid(IEnumerable<string> personas)
    {
        Componentes.EstiloTabla(grid);
        grid.Dock = DockStyle.Top;
        grid.ScrollBars = ScrollBars.None;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.EditMode = DataGridViewEditMode.EditOnEnter;
        grid.ShowCellErrors = true;
        grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;
        // Los textos largos se leen enteros: la fila crece con ellos
        grid.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders;
        grid.RowTemplate.MinimumHeight = 40;

        grid.Columns.Add(new DataGridViewCheckBoxColumn
        {
            HeaderText = "HECHA",
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 70,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        grid.Columns[ColHecha].Visible = !EsSeguimiento;
        grid.Columns.Add(ColumnaTexto(EsSeguimiento ? "COMPROBACIÓN" : "ACCIÓN", 40, 1000));
        grid.Columns.Add(ColumnaTexto("FECHA", 13, 10));

        colResponsable.HeaderText = "RESPONSABLE";
        colResponsable.FillWeight = 18;
        colResponsable.FlatStyle = FlatStyle.Flat;
        colResponsable.DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing;
        colResponsable.SortMode = DataGridViewColumnSortMode.NotSortable;
        colResponsable.Items.Add("");   // sin responsable
        foreach (string persona in personas)
            colResponsable.Items.Add(persona);
        grid.Columns.Add(colResponsable);

        grid.Columns.Add(ColumnaTexto("OBSERVACIONES", 29, 1000));
        grid.Columns.Add(new DataGridViewLinkColumn
        {
            HeaderText = "",
            Text = "Quitar",
            UseColumnTextForLinkValue = true,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            Width = 75,
            LinkColor = Tema.Error,
            ActiveLinkColor = Tema.Error,
            VisitedLinkColor = Tema.Error,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        foreach (int col in new[] { ColAccion, ColObservaciones })
            grid.Columns[col].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.Columns[ColFecha].DefaultCellStyle.NullValue = "";

        // La casilla cuenta en cuanto se marca, sin tener que salir de la celda
        grid.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (grid.IsCurrentCellDirty && grid.CurrentCell?.ColumnIndex == ColHecha)
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        grid.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex < 0 || cargando) return;
            grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "";
            AvisarCambio();
        };
        grid.CellEndEdit += (s, e) => FormatearFecha(e.RowIndex, e.ColumnIndex);
        grid.CellContentClick += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != ColQuitar) return;
            grid.Rows.RemoveAt(e.RowIndex);
            AjustarAlto();
            AvisarCambio();
        };
        grid.RowHeightChanged += (s, e) => AjustarAlto();
        // Un responsable que no esté en la lista no debe sacar el cuadro de error de Windows
        grid.DataError += (s, e) => e.ThrowException = false;
    }

    private static DataGridViewTextBoxColumn ColumnaTexto(string titulo, float peso, int maximo) => new()
    {
        HeaderText = titulo,
        FillWeight = peso,
        MaxInputLength = maximo,
        SortMode = DataGridViewColumnSortMode.NotSortable
    };

    // Filas

    public void Cargar(IEnumerable<AccionNc> acciones)
    {
        cargando = true;
        grid.Rows.Clear();
        foreach (var accion in acciones.Where(a => a.Clase == clase))
        {
            // Un responsable que ya no está en la lista se añade para no perderlo
            if (accion.Responsable != null && !colResponsable.Items.Contains(accion.Responsable))
                colResponsable.Items.Add(accion.Responsable);

            int i = grid.Rows.Add(accion.Hecha, accion.Accion, accion.Fecha?.ToString("dd/MM/yyyy") ?? "",
                accion.Responsable ?? "", accion.Observaciones ?? "");
            grid.Rows[i].Tag = accion.Id;
        }
        cargando = false;
        AjustarAlto();
    }

    private void AnadirFila()
    {
        grid.EndEdit();
        // Un seguimiento casi siempre se apunta el mismo día en que se hace
        string fecha = EsSeguimiento ? DateTime.Today.ToString("dd/MM/yyyy") : "";
        int i = grid.Rows.Add(false, "", fecha, "", "");
        grid.Rows[i].Tag = 0;
        AjustarAlto();
        AvisarCambio();

        grid.Focus();
        grid.CurrentCell = grid.Rows[i].Cells[ColAccion];
        grid.BeginEdit(true);
    }

    // Las filas vacías no cuentan. null si alguna está mal (el error queda en su celda).
    public List<AccionNc>? Leer(DateTime fechaDetectado)
    {
        grid.EndEdit();
        lblError.Visible = false;

        var acciones = new List<AccionNc>();
        bool hayErrores = false;

        foreach (DataGridViewRow fila in grid.Rows)
        {
            foreach (DataGridViewCell celda in fila.Cells)
                celda.ErrorText = "";

            if (FilaVacia(fila))
                continue;

            string accion = Texto(fila, ColAccion);
            string fechaTexto = Texto(fila, ColFecha);
            string responsable = Texto(fila, ColResponsable);
            string observaciones = Texto(fila, ColObservaciones);
            bool hecha = fila.Cells[ColHecha].Value is true;

            DateTime? fecha = null;
            if (fechaTexto != "")
            {
                if (DateTime.TryParseExact(fechaTexto, FormatosFecha, Componentes.Es, DateTimeStyles.None, out var leida))
                    fecha = leida;
                else
                {
                    fila.Cells[ColFecha].ErrorText = "Escribe la fecha como 20/06/2026.";
                    hayErrores = true;
                }
            }

            var leidaAccion = NoConformidadServicio.NormalizarAccion(new AccionNc(
                (int)(fila.Tag ?? 0), clase, accion, fecha, responsable, observaciones, hecha));

            if (NoConformidadServicio.ErrorAccion(leidaAccion, fechaDetectado) is string error)
            {
                int col = leidaAccion.Accion == "" ? ColAccion : ColFecha;
                if (fila.Cells[col].ErrorText == "") fila.Cells[col].ErrorText = error;
                hayErrores = true;
            }

            acciones.Add(leidaAccion);
        }

        if (hayErrores)
        {
            lblError.Text = "Revisa las filas marcadas en rojo (pasa el ratón por encima para ver el error).";
            lblError.Visible = true;
            return null;
        }
        return acciones;
    }

    // Sin comprobar nada, para saber en qué fase va el registro mientras se rellena
    public List<AccionNc> Borrador() =>
        grid.Rows.Cast<DataGridViewRow>()
            .Where(fila => !FilaVacia(fila))
            .Select(fila => new AccionNc(0, clase, Texto(fila, ColAccion), null, null, null,
                fila.Cells[ColHecha].Value is true))
            .ToList();

    private static bool FilaVacia(DataGridViewRow fila) =>
        fila.Cells[ColHecha].Value is not true
        && new[] { ColAccion, ColFecha, ColResponsable, ColObservaciones }.All(col => Texto(fila, col) == "");

    private static string Texto(DataGridViewRow fila, int col) =>
        (fila.Cells[col].Value?.ToString() ?? "").Trim();

    // Una fecha bien escrita se deja siempre como 20/06/2026
    private void FormatearFecha(int fila, int col)
    {
        if (fila < 0 || col != ColFecha) return;
        var celda = grid.Rows[fila].Cells[ColFecha];
        if (DateTime.TryParseExact(celda.Value?.ToString()?.Trim(), FormatosFecha, Componentes.Es,
                DateTimeStyles.None, out var fecha))
            celda.Value = fecha.ToString("dd/MM/yyyy");
    }

    // Sin scroll propio: la tabla mide lo que sus filas, dentro de la página que ya lo tiene
    private void AjustarAlto()
    {
        grid.Height = grid.ColumnHeadersHeight + grid.Rows.Cast<DataGridViewRow>().Sum(f => f.Height) + 2;
        lblVacia.Visible = grid.Rows.Count == 0;
    }

    private void AvisarCambio() => Cambiado?.Invoke(this, EventArgs.Empty);
}
