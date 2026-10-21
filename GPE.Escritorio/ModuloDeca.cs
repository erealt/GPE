using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Módulo DeCA: lista de documentos de control con sus filtros. Vive dentro de FormPrincipal.
public class ModuloDeca : UserControl
{
    private const int ColumnaPdf = 8;

    private readonly DecaServicio servicio = new();

    private readonly TextBox txtBuscar = new();
    private readonly DateTimePicker dtpDesde = new();
    private readonly DateTimePicker dtpHasta = new();
    private readonly Button btnLimpiar = Tema.BotonSecundario("Limpiar filtros", 140);
    private readonly Label lblContador = new();
    private readonly DataGridView grid = new();
    private readonly Label lblVacio = new();
    private readonly Button btnNuevo = Tema.BotonPrincipal("+  Nuevo DeCA", 190);
    private readonly Button btnAbrir = Tema.BotonSecundario("Abrir", 110);

    public ModuloDeca()
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
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 210));

        var titulos = new Panel { Dock = DockStyle.Fill };
        titulos.Controls.Add(new Label
        {
            Text = "Documentos de control del transporte, con su PDF y su código QR.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Suave
        });
        titulos.Controls.Add(new Label
        {
            Text = "DeCA",
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

        txtBuscar.Width = 340;
        txtBuscar.PlaceholderText = "Nº, remitente, destinatario o transportista";

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
        panel.Controls.Add(Componentes.CampoFiltro("Carga desde", dtpDesde));
        panel.Controls.Add(Componentes.CampoFiltro("Carga hasta", dtpHasta));
        panel.Controls.Add(btnLimpiar);

        // Los filtros se aplican al momento, sin botón de "Filtrar"
        txtBuscar.TextChanged += (s, e) => AplicarFiltros();
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

        btnAbrir.Height = 34;
        btnAbrir.Anchor = AnchorStyles.Right;
        btnAbrir.Margin = new Padding(0);
        btnAbrir.Click += (s, e) => { if (DecaSeleccionado() is DecaFila d) AbrirFormulario(d.Id); };

        barra.Controls.Add(lblContador, 0, 0);
        barra.Controls.Add(btnAbrir, 1, 0);
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

        string[] columnas =
        {
            "Nº DECA", "FECHA CARGA", "REMITENTE", "DESTINATARIO", "TRANSPORTISTA",
            "PALETS", "CAJAS", "PESO (KG)", "PDF"
        };
        foreach (string nombre in columnas)
            grid.Columns.Add(nombre, nombre);

        grid.Columns[0].DefaultCellStyle.ForeColor = Tema.Acento;
        grid.Columns[0].DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        grid.Columns[0].MinimumWidth = 140;   // el número entero, sin cortar
        grid.Columns[1].MinimumWidth = 120;
        foreach (int i in new[] { 2, 3, 4 }) grid.Columns[i].FillWeight = 150;
        foreach (int i in new[] { 5, 6, 7 })
        {
            grid.Columns[i].FillWeight = 70;
            grid.Columns[i].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns[i].HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            grid.Columns[i].DefaultCellStyle.Padding = new Padding(0, 0, 15, 0);
        }
        grid.Columns[7].MinimumWidth = 120;   // peso: "12.345,50"
        grid.Columns[ColumnaPdf].MinimumWidth = 130;

        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is DecaFila d) AbrirFormulario(d.Id);
        };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || DecaSeleccionado() is not DecaFila d) return;
            e.Handled = true;   // que Enter no baje a la fila siguiente
            AbrirFormulario(d.Id);
        };
        grid.SelectionChanged += (s, e) => btnAbrir.Enabled = DecaSeleccionado() != null;

        // Estado del PDF como etiqueta: verde si está al día, ámbar si hay que generarlo
        grid.CellPainting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != ColumnaPdf) return;
            if (grid.Rows[e.RowIndex].Tag is not DecaFila d) return;
            var (fondo, texto) = d.PdfAlDia
                ? Tema.ColorFase(Fase.AbonoRealizado)      // verde
                : Tema.ColorFase(Fase.RecogidaSolicitada); // ámbar
            Componentes.PintarEtiqueta(e, d.PdfAlDia ? "Al día" : "Por generar", fondo, texto);
        };

        return grid;
    }

    // ---------- Datos y filtros ----------

    private void CargarDatos(int? seleccionar = null)
    {
        List<DecaFila> decas;
        try
        {
            decas = servicio.ObtenerDecas();
        }
        catch (DbException ex)
        {
            MessageBox.Show(FindForm(),
                "No se pudieron cargar los DeCA de la base de datos.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        int? elegido = seleccionar ?? DecaSeleccionado()?.Id;

        grid.Rows.Clear();
        foreach (var d in decas)
        {
            int i = grid.Rows.Add(
                d.Numero,
                Componentes.Fecha(d.FechaCarga),
                d.Remitente,
                d.Destinatario,
                d.Transportista,
                d.Palets,
                d.Cajas.ToString("N0", Componentes.Es),
                d.PesoKg.ToString("N2", Componentes.Es),
                "");
            grid.Rows[i].Tag = d;
        }

        AplicarFiltros();
        if (elegido != null) Seleccionar(elegido.Value);
    }

    private void AplicarFiltros()
    {
        string texto = txtBuscar.Text.Trim();
        DateTime? desde = dtpDesde.Checked ? dtpDesde.Value.Date : null;
        DateTime? hasta = dtpHasta.Checked ? dtpHasta.Value.Date : null;

        grid.CurrentCell = null;

        foreach (DataGridViewRow fila in grid.Rows)
        {
            if (fila.Tag is not DecaFila d) continue;

            bool coincideTexto = texto == ""
                || new[] { d.Numero, d.Remitente, d.Destinatario, d.Transportista }
                    .Any(v => v.Contains(texto, StringComparison.OrdinalIgnoreCase));
            bool coincideFecha = (desde == null || d.FechaCarga >= desde)
                              && (hasta == null || d.FechaCarga <= hasta);

            fila.Visible = coincideTexto && coincideFecha;
        }

        bool fechasCruzadas = desde != null && hasta != null && desde > hasta;
        int visibles = grid.Rows.Cast<DataGridViewRow>().Count(f => f.Visible);
        lblContador.Text = $"{visibles} {(visibles == 1 ? "documento" : "documentos")}";

        lblVacio.Text = fechasCruzadas
            ? "La fecha \"desde\" es posterior a la fecha \"hasta\"."
            : grid.Rows.Count == 0
                ? "Todavía no hay ningún DeCA. Crea el primero con \"+ Nuevo DeCA\"."
                : "No hay ningún DeCA que coincida con los filtros.";
        lblVacio.ForeColor = fechasCruzadas ? Tema.Error : Tema.Suave;
        lblVacio.Visible = visibles == 0;
        grid.Visible = visibles > 0;
        btnAbrir.Enabled = DecaSeleccionado() != null;
    }

    private void LimpiarFiltros()
    {
        txtBuscar.Clear();
        dtpDesde.Checked = false;
        dtpHasta.Checked = false;
        AplicarFiltros();
    }

    private void Seleccionar(int id)
    {
        var fila = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(f => f.Tag is DecaFila d && d.Id == id);
        if (fila == null) return;
        if (!fila.Visible) LimpiarFiltros();

        grid.ClearSelection();
        grid.CurrentCell = fila.Cells[0];
        fila.Selected = true;
        grid.FirstDisplayedScrollingRowIndex = fila.Index;
    }

    private DecaFila? DecaSeleccionado() =>
        grid.SelectedRows.Cast<DataGridViewRow>()
            .FirstOrDefault(f => f.Visible && f.Tag is DecaFila)?.Tag as DecaFila;

    // null = nuevo DeCA
    private void AbrirFormulario(int? decaId)
    {
        DecaGuardado? existente = null;
        if (decaId != null)
        {
            try
            {
                existente = servicio.ObtenerDeca(decaId.Value);
            }
            catch (DbException ex)
            {
                MessageBox.Show(FindForm(), "No se pudo cargar el DeCA.\n\n" + ex.Message,
                    "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (existente == null)
            {
                MessageBox.Show(FindForm(), "Este DeCA ya no existe.", "DeCA",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                CargarDatos();
                return;
            }
        }

        using var formulario = new FormDeca(existente);
        bool guardado = formulario.ShowDialog(FindForm()) == DialogResult.OK;
        CargarDatos(guardado ? formulario.IdGuardado : decaId);
    }
}
