using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Módulo de calidad: la lista de incidencias, no conformidades y acciones de mejora
// (la hoja INCIDENCIAS del Excel) con sus filtros. Vive dentro de FormPrincipal.
public class ModuloNoConformidades : UserControl
{
    private const int ColumnaTipo = 2;
    private const int ColumnaProblema = 4;
    private const int ColumnaEstado = 7;

    private const string TodasLasSecciones = "Todas";
    private const string EstadoAbiertas = "Abiertas";

    private readonly NoConformidadServicio servicio = new();
    private List<NoConformidad> registros = new();
    private bool limpiando;

    private readonly TextBox txtBuscar = new();
    private readonly ComboBox cmbTipo = new();
    private readonly ComboBox cmbSeccion = new();
    private readonly ComboBox cmbEstado = new();
    private readonly DateTimePicker dtpDesde = new();
    private readonly DateTimePicker dtpHasta = new();
    private readonly Button btnLimpiar = Tema.BotonSecundario("Limpiar filtros", 140);
    private readonly Label lblContador = new();
    private readonly DataGridView grid = new();
    private readonly Label lblVacio = new();
    private readonly Button btnNuevo = Tema.BotonPrincipal("+  Nuevo registro", 210);

    // Opciones del filtro de tipo: null = todos
    private record OpcionTipo(string? Codigo)
    {
        public override string ToString() => Codigo == null ? "Todos" : TipoRegistro.Nombre(Codigo);
    }

    public ModuloNoConformidades()
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
            Text = "Registro de calidad de incidencias, no conformidades y acciones de mejora.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Suave
        };
        var lblTitulo = new Label
        {
            Text = "Incidencias y no conformidades",
            Dock = DockStyle.Top,
            Height = 50,
            Font = Tema.FuenteTitulo,
            ForeColor = Tema.Texto
        };
        titulos.Controls.Add(lblSubtitulo);
        titulos.Controls.Add(lblTitulo);

        btnNuevo.Height = 46;
        btnNuevo.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnNuevo.Margin = new Padding(0, 10, 0, 0);
        btnNuevo.Click += (s, e) => AbrirRegistro(null);

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

        // Anchos pensados para que todos los filtros quepan en una fila con la ventana a su tamaño normal
        txtBuscar.Width = 210;
        txtBuscar.PlaceholderText = "Nº, problema, cliente...";

        cmbTipo.Width = 150;
        cmbTipo.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbTipo.Items.Add(new OpcionTipo(null));
        foreach (string tipo in TipoRegistro.Todos)
            cmbTipo.Items.Add(new OpcionTipo(tipo));
        cmbTipo.SelectedIndex = 0;

        cmbSeccion.Width = 160;
        cmbSeccion.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbSeccion.Items.Add(TodasLasSecciones);
        cmbSeccion.SelectedIndex = 0;

        cmbEstado.Width = 140;
        cmbEstado.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbEstado.Items.Add("Todos");
        cmbEstado.Items.Add(EstadoAbiertas);
        cmbEstado.Items.AddRange(EstadoRegistro.Todos);
        cmbEstado.SelectedIndex = 0;

        foreach (var fecha in new[] { dtpDesde, dtpHasta })
        {
            fecha.Width = 135;
            fecha.Format = DateTimePickerFormat.Short;
            fecha.ShowCheckBox = true;
            fecha.Checked = false;
        }

        panel.Controls.Add(Componentes.CampoFiltro("Buscar", txtBuscar));
        panel.Controls.Add(Componentes.CampoFiltro("Tipo", cmbTipo));
        panel.Controls.Add(Componentes.CampoFiltro("Sección", cmbSeccion));
        panel.Controls.Add(Componentes.CampoFiltro("Estado", cmbEstado));
        panel.Controls.Add(Componentes.CampoFiltro("Detectado desde", dtpDesde));
        panel.Controls.Add(Componentes.CampoFiltro("Detectado hasta", dtpHasta));

        // Los filtros se aplican al momento, sin botón de "Filtrar"
        txtBuscar.TextChanged += (s, e) => AplicarFiltros();
        cmbTipo.SelectedIndexChanged += (s, e) => AplicarFiltros();
        cmbSeccion.SelectedIndexChanged += (s, e) => AplicarFiltros();
        cmbEstado.SelectedIndexChanged += (s, e) => AplicarFiltros();
        dtpDesde.ValueChanged += (s, e) => AplicarFiltros();
        dtpHasta.ValueChanged += (s, e) => AplicarFiltros();

        return panel;
    }

    // Contador de resultados y botón de limpiar filtros (los filtros no caben en una fila con él)
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

        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is NoConformidad nc)
                AbrirRegistro(nc);
        };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || grid.CurrentRow?.Tag is not NoConformidad nc) return;
            e.Handled = true;   // si no, Enter baja a la fila siguiente
            AbrirRegistro(nc);
        };
        grid.CellPainting += PintarEtiquetas;

        grid.ColumnHeadersHeight = 45;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);

        // Ancho fijo para las columnas cortas; el problema se queda con el resto
        (string Nombre, int Ancho)[] columnas =
        {
            ("Nº", 70), ("DETECTADA", 110), ("TIPO", 115), ("SECCIÓN", 145),
            ("PROBLEMA", 0), ("RESPONSABLE", 125), ("PREVISTA", 100), ("ESTADO", 140)
        };
        foreach (var (nombre, ancho) in columnas)
        {
            int i = grid.Columns.Add(nombre, nombre);
            if (ancho == 0) continue;
            grid.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns[i].Width = ancho;
        }

        grid.Columns[0].DefaultCellStyle.ForeColor = Tema.Acento;
        grid.Columns[0].DefaultCellStyle.Font = Tema.FuenteNegrita;
        grid.Columns[ColumnaProblema].MinimumWidth = 200;
        grid.Columns[ColumnaProblema].DefaultCellStyle.WrapMode = DataGridViewTriState.False;

        return grid;
    }

    // Tipo y estado se dibujan como etiquetas de color
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

    // Datos y filtros

    private void CargarDatos()
    {
        try
        {
            registros = servicio.ObtenerRegistros();

            if (cmbSeccion.Items.Count == 1)
            {
                var secciones = servicio.ObtenerListas().GetValueOrDefault(ListaCalidad.Seccion) ?? new();
                foreach (var seccion in secciones)
                    cmbSeccion.Items.Add(seccion.Valor);
            }
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudieron cargar las incidencias y no conformidades de la base de datos.", ex);
            return;
        }

        // Secciones antiguas que ya no están en la lista, para poder filtrar por ellas
        foreach (string seccion in registros.Select(r => r.Seccion).OfType<string>().Distinct())
            if (!cmbSeccion.Items.Contains(seccion))
                cmbSeccion.Items.Add(seccion);

        AplicarFiltros();
    }

    private static DataGridViewRow CrearFila(DataGridView grid, NoConformidad nc)
    {
        var fila = new DataGridViewRow { Tag = nc };
        // En la lista solo cabe la primera línea del problema; entero, al pasar el ratón
        string problema = nc.Problema.Split('\n')[0].Trim();

        // Fechas cortas: en la lista no caben como "29 sept 2026" (ni con el año entero)
        static string Corta(DateTime? fecha) => fecha?.ToString("dd/MM/yy") ?? "—";

        fila.CreateCells(grid,
            nc.Numero ?? "—",
            Corta(nc.FechaDetectado),
            nc.TipoRegistro,
            nc.Seccion ?? "—",
            problema,
            nc.Responsable ?? "—",
            Corta(nc.FechaPrevista),
            NoConformidadServicio.Estado(nc));

        fila.Cells[ColumnaProblema].ToolTipText = nc.Problema.Length > 600 ? nc.Problema[..600] + "…" : nc.Problema;

        if (NoConformidadServicio.DiasFueraPlazo(nc) is int dias)
            fila.Cells[ColumnaEstado].ToolTipText = $"La fecha prevista pasó hace {dias} {(dias == 1 ? "día" : "días")}";

        return fila;
    }

    // Con más de mil registros es mucho más rápido rehacer la tabla que ocultar filas
    private void AplicarFiltros(int? seleccionar = null)
    {
        if (limpiando) return;
        var visibles = Filtrados();

        grid.Rows.Clear();
        grid.Rows.AddRange(visibles.Select(nc => CrearFila(grid, nc)).ToArray());
        grid.ClearSelection();

        // Primero el contador, que vuelve a mostrar la tabla si estaba oculta (lista vacía):
        // en una tabla oculta no se puede desplazar hasta una fila
        ActualizarContador(visibles, dtpDesde.Checked && dtpHasta.Checked && dtpDesde.Value.Date > dtpHasta.Value.Date);

        if (seleccionar != null)
            Seleccionar(seleccionar.Value);
    }

    private List<NoConformidad> Filtrados()
    {
        string texto = txtBuscar.Text.Trim();
        string? tipo = (cmbTipo.SelectedItem as OpcionTipo)?.Codigo;
        string? seccion = cmbSeccion.SelectedIndex > 0 ? cmbSeccion.Text : null;
        string? estado = cmbEstado.SelectedIndex > 0 ? cmbEstado.Text : null;
        DateTime? desde = dtpDesde.Checked ? dtpDesde.Value.Date : null;
        DateTime? hasta = dtpHasta.Checked ? dtpHasta.Value.Date : null;

        bool Contiene(string? campo) => campo?.Contains(texto, StringComparison.CurrentCultureIgnoreCase) == true;

        return registros.Where(nc =>
                (texto == "" || Contiene(nc.Numero) || Contiene(nc.Problema) || Contiene(nc.Cliente)
                    || Contiene(nc.Proveedor) || Contiene(nc.Responsable))
                && (tipo == null || nc.TipoRegistro == tipo)
                && (seccion == null || nc.Seccion == seccion)
                && (estado == null
                    || (estado == EstadoAbiertas ? NoConformidadServicio.Abierta(nc) : NoConformidadServicio.Estado(nc) == estado))
                && (desde == null || nc.FechaDetectado >= desde)
                && (hasta == null || nc.FechaDetectado <= hasta))
            .ToList();
    }

    private void Seleccionar(int id)
    {
        var fila = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(f => f.Tag is NoConformidad nc && nc.Id == id);
        if (fila == null) return;

        grid.CurrentCell = fila.Cells[0];
        fila.Selected = true;
        // Sin espacio para filas (ventana minimizada, tabla aún sin medir) no se puede desplazar
        if (grid.Visible && grid.DisplayRectangle.Height > grid.ColumnHeadersHeight)
            grid.FirstDisplayedScrollingRowIndex = fila.Index;
    }

    private void LimpiarFiltros()
    {
        // Cada control que se limpia lanzaría AplicarFiltros: se rehace la tabla una sola vez al final
        limpiando = true;
        txtBuscar.Clear();
        cmbTipo.SelectedIndex = 0;
        cmbSeccion.SelectedIndex = 0;
        cmbEstado.SelectedIndex = 0;
        dtpDesde.Checked = false;
        dtpHasta.Checked = false;
        limpiando = false;
        AplicarFiltros();
    }

    private void ActualizarContador(List<NoConformidad> visibles, bool fechasCruzadas)
    {
        int abiertas = visibles.Count(NoConformidadServicio.Abierta);
        int fuera = visibles.Count(nc => NoConformidadServicio.Estado(nc) == EstadoRegistro.FueraPlazo);
        lblContador.Text = $"{visibles.Count} {(visibles.Count == 1 ? "registro" : "registros")}"
            + $"  ·  {abiertas} {(abiertas == 1 ? "abierto" : "abiertos")}"
            + (fuera > 0 ? $"  ·  {fuera} fuera de plazo" : "");

        lblVacio.Text = fechasCruzadas
            ? "La fecha \"desde\" es posterior a la fecha \"hasta\"."
            : registros.Count == 0
                ? "Todavía no hay registros. Crea el primero con \"+ Nuevo registro\"."
                : "No hay registros que coincidan con los filtros.";
        lblVacio.ForeColor = fechasCruzadas ? Tema.Error : Tema.Suave;
        lblVacio.Visible = visibles.Count == 0;
        grid.Visible = visibles.Count > 0;
    }

    // Abre el registro (o uno nuevo con null) y, si se guarda, recarga la lista y lo selecciona
    private void AbrirRegistro(NoConformidad? nc)
    {
        if (nc != null)
        {
            try
            {
                // Se vuelve a leer por si otro usuario lo ha cambiado desde que se cargó la lista
                nc = servicio.ObtenerRegistro(nc.Id);
            }
            catch (DbException ex)
            {
                Mensajes.ErrorBaseDatos(this, "No se pudo cargar el registro.", ex);
                return;
            }
            if (nc == null)
            {
                MessageBox.Show(this, "Este registro ya no existe: puede que otro usuario lo haya borrado.",
                    "Registro no encontrado", MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarDatos();
                return;
            }
        }

        int id;
        using (var formulario = new FormNoConformidad(nc))
        {
            if (formulario.ShowDialog(this) != DialogResult.OK) return;
            id = formulario.IdGuardado;
        }

        try
        {
            registros = servicio.ObtenerRegistros();
        }
        catch (DbException)
        {
            return;   // se guardó; la lista se actualizará la próxima vez
        }

        // Si el registro guardado no pasa los filtros, se quitan para que se vea
        if (!Filtrados().Any(r => r.Id == id)) LimpiarFiltros();
        AplicarFiltros(id);
    }
}
