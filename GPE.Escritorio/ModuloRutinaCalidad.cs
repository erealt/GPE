using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace GPE.Escritorio;

// Rutina diaria del supervisor de calidad (la hoja "CHEQUEOS DIARIOS - PCC"): una tabla
// con los puntos de control en filas y las máquinas en columnas. Se marcan las casillas
// con el teclado (varias a la vez) o con el botón derecho, y desde una casilla "no
// correcta" se abre una incidencia. Vive dentro de FormPrincipal.
public class ModuloRutinaCalidad : UserControl
{
    private const int AnchoPunto = 220;
    private const int AnchoSeccion = 58;

    // La de las cabeceras en Componentes.EstiloTabla
    private static readonly Font FuenteCabecera = new("Segoe UI", 8.5f, FontStyle.Bold);

    // Las cabeceras se parten por palabras: en la columna tiene que caber la más larga (DEPOSITOS)
    public static int AnchoColumna(string nombre) =>
        Math.Max(AnchoSeccion, nombre.Split(' ').Max(palabra => TextRenderer.MeasureText(palabra, FuenteCabecera).Width) + 26);

    private readonly RutinaServicio servicio = new();
    private PlantillaRutina plantilla = new(new(), new(), new());
    private Rutina rutina = new(0, DateTime.Today, null, null, null, null);
    // Casillas rellenas de la ronda que se está viendo, por (punto, sección)
    private Dictionary<(int PuntoId, int SeccionId), ResultadoRutina> casillas = new();
    private List<ObservacionRutina> observaciones = new();
    private bool hayCambios;
    private bool cargando;

    private readonly DateTimePicker dtpFecha = new();
    // Cada planta se ve por separado: una pestaña por planta
    private string plantaElegida = Planta.Baja;
    private readonly Label lblRonda = new();
    private readonly Label lblProgreso = new();
    private readonly DataGridView grid = new();
    private readonly Button btnGuardar = Tema.BotonPrincipal("Guardar ronda", 170);

    private static readonly string[] Plantas = { Planta.Baja, Planta.Superior };

    // Observaciones del pie de la hoja, en su propia pestaña: caja de herramientas,
    // depósitos y una lista de sección + observación
    private readonly RadioButton pestanaObservaciones = new();
    private Control panelObservaciones = null!;
    private readonly ComboBox cmbCajaOperario = new();
    private readonly CheckBox btnCajaOk = new(), btnCajaNoOk = new();   // estado de la caja: uno, otro o ninguno
    private readonly TextBox txtDepositos = new();
    private readonly DataGridView gridObservaciones = new();
    private readonly DataGridViewComboBoxColumn columnaSeccion = new();

    public ModuloRutinaCalidad()
    {
        Dock = DockStyle.Fill;
        Controls.Add(CrearContenido());
        Load += (s, e) =>
        {
            CargarOperarios();
            Cargar(DateTime.Today, recargarPlantilla: true);
            // La ventana principal no sabe de esta ronda: avisa ella al cerrar con cambios
            if (FindForm() is Form ventana) ventana.FormClosing += AvisarAlCerrar;
        };
    }

    private Control CrearContenido()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 5,
            Padding = new Padding(40, 30, 40, 20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // La tabla y las observaciones ocupan el mismo sitio: se ve una u otra según la pestaña
        var zona = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        panelObservaciones = CrearObservaciones();
        panelObservaciones.Visible = false;
        zona.Controls.Add(panelObservaciones);
        zona.Controls.Add(ConfigurarGrid());

        layout.Controls.Add(CrearCabecera(), 0, 0);
        layout.Controls.Add(CrearBarra(), 0, 1);
        layout.Controls.Add(CrearLeyenda(), 0, 2);
        layout.Controls.Add(CrearPestanasPlanta(), 0, 3);
        layout.Controls.Add(zona, 0, 4);
        return layout;
    }

    private Control CrearCabecera()
    {
        var cabecera = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titulos = new Panel { Dock = DockStyle.Fill };
        titulos.Controls.Add(new Label
        {
            Text = "Chequeos diarios y PCC al recorrer la fábrica.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Suave
        });
        titulos.Controls.Add(new Label
        {
            Text = "Rutina supervisor de calidad",
            Dock = DockStyle.Top,
            Height = 50,
            Font = Tema.FuenteTitulo,
            ForeColor = Tema.Texto
        });

        btnGuardar.Height = 46;
        btnGuardar.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnGuardar.Margin = new Padding(0, 10, 0, 0);
        btnGuardar.Enabled = false;
        btnGuardar.Click += (s, e) => Guardar();

        cabecera.Controls.Add(titulos, 0, 0);
        cabecera.Controls.Add(btnGuardar, 1, 0);
        return cabecera;
    }

    private Control CrearBarra()
    {
        var barra = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            BackColor = Color.White,
            Padding = new Padding(15, 8, 15, 0)
        };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        dtpFecha.Width = 135;
        dtpFecha.Format = DateTimePickerFormat.Short;
        dtpFecha.MaxDate = DateTime.Today;
        dtpFecha.ValueChanged += (s, e) => { if (!cargando) CambiarDia(dtpFecha.Value.Date); };

        Button Flecha(string texto, int dias)
        {
            var boton = Tema.BotonSecundario(texto, 40);
            boton.Height = 32;
            boton.Margin = new Padding(0, 22, 6, 0);
            boton.Click += (s, e) =>
            {
                var dia = dtpFecha.Value.Date.AddDays(dias);
                if (dia <= dtpFecha.MaxDate) CambiarDia(dia);
            };
            return boton;
        }
        var btnHoy = Tema.BotonSecundario("Hoy", 60);
        btnHoy.Height = 32;
        btnHoy.Margin = new Padding(0, 22, 20, 0);
        btnHoy.Click += (s, e) => CambiarDia(DateTime.Today);

        var filtros = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        filtros.Controls.Add(Componentes.CampoFiltro("Día", dtpFecha));
        filtros.Controls.Add(Flecha("◀", -1));
        filtros.Controls.Add(Flecha("▶", 1));
        filtros.Controls.Add(btnHoy);

        var datos = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(10, 6, 0, 0)
        };
        lblRonda.AutoSize = true;
        lblRonda.ForeColor = Tema.Texto;
        lblRonda.Font = Tema.FuenteNegrita;
        lblProgreso.AutoSize = true;
        lblProgreso.ForeColor = Tema.Suave;
        lblProgreso.Margin = new Padding(0, 4, 0, 0);
        datos.Controls.Add(lblRonda);
        datos.Controls.Add(lblProgreso);

        var btnRecargar = Tema.BotonSecundario("Recargar", 110);
        btnRecargar.Height = 34;
        btnRecargar.Click += (s, e) =>
        {
            if (DescartarCambios()) Cargar(rutina.Fecha, recargarPlantilla: true);
        };
        var btnPlantilla = Tema.BotonSecundario("Plantilla", 110);
        btnPlantilla.Height = 34;
        btnPlantilla.Margin = new Padding(10, 3, 0, 3);
        btnPlantilla.Click += (s, e) => ConfigurarPlantilla();

        var botones = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 14, 0, 0) };
        botones.Controls.Add(btnRecargar);
        botones.Controls.Add(btnPlantilla);

        barra.Controls.Add(filtros, 0, 0);
        barra.Controls.Add(datos, 1, 0);
        barra.Controls.Add(botones, 2, 0);
        return barra;
    }

    // Colores de la tabla y teclas para marcar
    private static Control CrearLeyenda()
    {
        var leyenda = new FlowLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 0) };

        foreach (string estado in EstadoCasilla.Todos)
        {
            var (fondo, texto) = Tema.ColorEstadoCasilla(estado);
            leyenda.Controls.Add(new Label
            {
                Text = $"{Simbolo(estado)}  {EstadoCasilla.Nombre(estado)} ({Tecla(estado)})",
                AutoSize = true,
                BackColor = fondo,
                ForeColor = texto,
                Font = Tema.FuenteEtiqueta,
                Padding = new Padding(6, 3, 6, 3),
                Margin = new Padding(0, 0, 8, 0)
            });
        }
        var noRevisa = new Label
        {
            Text = "No se revisa",
            AutoSize = true,
            BackColor = Tema.FondoNoRevisa,
            ForeColor = Tema.Suave,
            Font = Tema.FuenteEtiqueta,
            Padding = new Padding(6, 3, 6, 3),
            Margin = new Padding(0, 0, 8, 0)
        };
        leyenda.Controls.Add(noRevisa);
        leyenda.SetFlowBreak(noRevisa, true);
        leyenda.Controls.Add(new Label
        {
            Text = "Pulsa una casilla para marcarla. Para varias, selecciónalas (o pulsa una cabecera) " +
                   "y pulsa su letra. En PESOS se escribe el número.",
            AutoSize = true,
            ForeColor = Tema.Suave,
            Font = Tema.FuentePequena,
            Margin = new Padding(0, 6, 0, 0)
        });
        return leyenda;
    }

    // Como el pie de la hoja: caja de herramientas, depósitos y observaciones por sección
    private Control CrearObservaciones()
    {
        var (scroll, pila) = Componentes.Pagina();
        scroll.Padding = new Padding(0, 0, 10, 0);
        var campos = new CamposFormulario();

        foreach (var combo in new[] { cmbCajaOperario })
        {
            combo.DropDownStyle = ComboBoxStyle.DropDown;   // se elige de la lista o se escribe
            combo.MaxLength = RutinaServicio.LargoCaja;
            combo.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
            combo.AutoCompleteSource = AutoCompleteSource.ListItems;
            combo.TextChanged += (s, e) => { if (!cargando) MarcarCambios(); };
        }

        var caja = Componentes.Tarjeta("Caja de herramientas", "Quién la tiene hoy y cómo está.");
        var rejilla = Componentes.Rejilla(2);
        rejilla.Controls.Add(campos.Campo("Operario", cmbCajaOperario));
        rejilla.Controls.Add(campos.Campo("Estado", CrearEstadoCaja()));
        caja.Controls.Add(rejilla);
        pila.Controls.Add(caja);

        txtDepositos.Multiline = true;
        txtDepositos.Height = 70;
        txtDepositos.ScrollBars = ScrollBars.Vertical;
        txtDepositos.MaxLength = RutinaServicio.LargoComentario;
        txtDepositos.PlaceholderText = "Por ejemplo: El nº8 está a 40 (rango: 42-48).";
        txtDepositos.Dock = DockStyle.Top;
        txtDepositos.TextChanged += (s, e) => { if (!cargando) MarcarCambios(); };
        var depositos = Componentes.Tarjeta("Depósitos", "Temperaturas fuera de rango y lo que haya que contar de los depósitos.");
        depositos.Controls.Add(txtDepositos);
        pila.Controls.Add(depositos);

        var secciones = Componentes.Tarjeta("Observaciones por sección",
            "Elige la sección en la última fila y escribe la observación. Para quitar una, selecciónala y pulsa Supr.");
        secciones.Controls.Add(ConfigurarGridObservaciones());
        pila.Controls.Add(secciones);

        return scroll;
    }

    private Control ConfigurarGridObservaciones()
    {
        Componentes.EstiloTabla(gridObservaciones, altoFila: 36);
        gridObservaciones.Dock = DockStyle.Top;
        gridObservaciones.Height = 260;
        gridObservaciones.AllowUserToAddRows = true;      // la fila en blanco del final es para añadir
        gridObservaciones.AllowUserToDeleteRows = true;
        gridObservaciones.EditMode = DataGridViewEditMode.EditOnEnter;
        gridObservaciones.GridColor = Tema.LineaTabla;
        gridObservaciones.CellBorderStyle = DataGridViewCellBorderStyle.Single;

        columnaSeccion.HeaderText = "SECCIÓN";
        columnaSeccion.Width = 200;
        columnaSeccion.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
        columnaSeccion.FlatStyle = FlatStyle.Flat;
        columnaSeccion.DisplayMember = nameof(RutinaSeccion.Nombre);
        columnaSeccion.ValueMember = nameof(RutinaSeccion.Id);
        gridObservaciones.Columns.Add(columnaSeccion);
        gridObservaciones.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "OBSERVACIÓN",
            MaxInputLength = RutinaServicio.LargoComentario
        });

        // La sección se apunta en cuanto se elige, sin esperar a salir de la celda
        gridObservaciones.CurrentCellDirtyStateChanged += (s, e) =>
        {
            if (gridObservaciones.CurrentCell is DataGridViewComboBoxCell)
                gridObservaciones.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        gridObservaciones.CellValueChanged += (s, e) => { if (!cargando) MarcarCambios(); };
        gridObservaciones.UserDeletedRow += (s, e) => MarcarCambios();
        // Una sección que ya no está en la plantilla no rompe la tabla: se queda en blanco
        gridObservaciones.DataError += (s, e) => e.ThrowException = false;
        return gridObservaciones;
    }

    // Los operarios de la lista de calidad (los de las incidencias), para elegir el de la caja
    private void CargarOperarios()
    {
        try
        {
            var operarios = new NoConformidadServicio().ObtenerListas().GetValueOrDefault(ListaCalidad.Operario) ?? new();
            cmbCajaOperario.Items.AddRange(operarios.Select(o => (object)o.Valor).ToArray());
        }
        catch (DbException)
        {
            // Sin la lista se puede escribir el nombre igual
        }
    }

    private List<ObservacionRutina> LeerObservaciones() =>
        gridObservaciones.Rows.Cast<DataGridViewRow>()
            .Where(f => !f.IsNewRow)
            .Select(f => new ObservacionRutina(f.Cells[0].Value as int? ?? 0, f.Cells[1].Value as string ?? ""))
            .ToList();

    private void MostrarObservaciones(bool ver)
    {
        grid.EndEdit();
        gridObservaciones.EndEdit();
        panelObservaciones.Visible = ver;
        grid.Visible = !ver;
    }

    // ---------- Tabla ----------

    private Control ConfigurarGrid()
    {
        Componentes.EstiloTabla(grid, altoFila: 32);
        grid.Dock = DockStyle.Fill;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.GridColor = Tema.LineaTabla;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.AllowUserToResizeColumns = false;
        grid.AllowUserToOrderColumns = false;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.MultiSelect = true;
        grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        grid.ShowCellToolTips = true;
        grid.ColumnHeadersHeight = 76;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;

        grid.CellPainting += PintarCasilla;
        grid.CellMouseEnter += (s, e) => SeguirRaton(e.RowIndex, e.ColumnIndex);
        grid.MouseLeave += (s, e) => SeguirRaton(-1, -1);
        grid.KeyDown += TeclaPulsada;
        grid.CellMouseDown += BotonDerecho;
        // Al pulsar una sola casilla salen las opciones de la leyenda. Si se arrastra o se usa
        // Ctrl / Mayús para elegir varias, no: se marcan después con una letra o el botón derecho.
        grid.CellMouseClick += (s, e) =>
        {
            if (e.Button == MouseButtons.Left && ModifierKeys == Keys.None && grid.SelectedCells.Count == 1
                && Casilla(e.RowIndex, e.ColumnIndex) is var (punto, _) && !punto.Numerico)
            {
                var celda = grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
                MostrarOpciones(e.RowIndex, e.ColumnIndex, new Point(celda.Left, celda.Bottom));
            }
        };
        grid.CellClick += (s, e) =>
        {
            if (e.ColumnIndex == 0 && e.RowIndex >= 0) SeleccionarFila(e.RowIndex);
        };
        grid.ColumnHeaderMouseClick += (s, e) => { if (e.ColumnIndex > 0) SeleccionarColumna(e.ColumnIndex); };
        grid.CellToolTipTextNeeded += (s, e) => e.ToolTipText = TextoAyuda(e.RowIndex, e.ColumnIndex);
        grid.CellValidating += ValidarNumero;
        grid.CellEndEdit += GuardarNumero;

        return grid;
    }

    // Rehace la tabla con la plantilla y la planta elegida, y la rellena con las casillas
    private void Construir()
    {
        grid.SuspendLayout();
        grid.Rows.Clear();
        grid.Columns.Clear();

        var columnaPunto = new DataGridViewTextBoxColumn
        {
            HeaderText = "PUNTO DE CONTROL",
            Width = AnchoPunto,
            Frozen = true,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        columnaPunto.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        columnaPunto.DefaultCellStyle.Padding = new Padding(10, 0, 4, 0);
        columnaPunto.DefaultCellStyle.BackColor = Tema.CabeceraTabla;
        columnaPunto.DefaultCellStyle.SelectionBackColor = Tema.CabeceraTabla;
        columnaPunto.DefaultCellStyle.Font = Tema.FuentePequenaNegrita;
        columnaPunto.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleLeft;
        grid.Columns.Add(columnaPunto);

        var secciones = SeccionesDe(plantaElegida);
        foreach (var seccion in secciones)
        {
            var columna = new DataGridViewTextBoxColumn
            {
                HeaderText = seccion.Nombre,
                Width = AnchoColumna(seccion.Nombre),
                Tag = seccion,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            columna.HeaderCell.ToolTipText = $"{seccion.Nombre} · {Planta.Nombre(seccion.Planta)}";
            grid.Columns.Add(columna);
        }

        // Los puntos que no se revisan en ninguna máquina de esta planta no salen
        foreach (var punto in plantilla.Puntos.Where(p => secciones.Any(s => Editable(p, s))))
        {
            var fila = new DataGridViewRow { Tag = punto };
            fila.CreateCells(grid);
            fila.Cells[0].Value = punto.Nombre;
            for (int c = 1; c < grid.Columns.Count; c++)
            {
                var seccion = (RutinaSeccion)grid.Columns[c].Tag!;
                // Solo se escriben a mano los números; lo demás se marca con teclas o el menú
                fila.Cells[c].ReadOnly = !(punto.Numerico && Editable(punto, seccion));
                if (punto.Numerico)
                    fila.Cells[c].Value = casillas.GetValueOrDefault((punto.Id, seccion.Id))?.Valor?.ToString("0.##", Componentes.Es);
            }
            grid.Rows.Add(fila);
        }

        grid.ResumeLayout();
        grid.ClearSelection();
    }

    private List<RutinaSeccion> SeccionesDe(string planta) =>
        plantilla.Secciones.Where(s => s.Planta == planta).ToList();

    // Pestañas encima de la tabla, con el mismo aspecto que el selector de tipo de las incidencias
    private Control CrearPestanasPlanta()
    {
        var pestanas = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
        foreach (string planta in Plantas)
        {
            var boton = new RadioButton
            {
                Text = Planta.Nombre(planta),
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(260, 36),
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                Checked = planta == plantaElegida
            };
            boton.CheckedChanged += (s, e) =>
            {
                PintarPestana(boton);
                if (!boton.Checked) return;
                MostrarObservaciones(false);
                plantaElegida = planta;
                Construir();
            };
            PintarPestana(boton);
            pestanas.Controls.Add(boton);
        }

        pestanaObservaciones.Appearance = Appearance.Button;
        pestanaObservaciones.FlatStyle = FlatStyle.Flat;
        pestanaObservaciones.TextAlign = ContentAlignment.MiddleCenter;
        pestanaObservaciones.Size = new Size(200, 36);
        pestanaObservaciones.Margin = new Padding(0, 0, 6, 0);
        pestanaObservaciones.Cursor = Cursors.Hand;
        pestanaObservaciones.Text = "Observaciones";
        pestanaObservaciones.CheckedChanged += (s, e) =>
        {
            PintarPestana(pestanaObservaciones);
            if (pestanaObservaciones.Checked) MostrarObservaciones(true);
        };
        PintarPestana(pestanaObservaciones);
        pestanas.Controls.Add(pestanaObservaciones);
        return pestanas;
    }

    // Dos botones en vez de una lista: se pulsa uno y, si se vuelve a pulsar, se desmarca
    private Control CrearEstadoCaja()
    {
        var botones = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = Padding.Empty };
        foreach (var (boton, texto) in new[] { (btnCajaOk, "✓  OK"), (btnCajaNoOk, "✗  No OK") })
        {
            boton.Text = texto;
            boton.Appearance = Appearance.Button;
            boton.FlatStyle = FlatStyle.Flat;
            boton.TextAlign = ContentAlignment.MiddleCenter;
            boton.Size = new Size(130, 34);
            boton.Margin = new Padding(0, 0, 10, 0);
            boton.Cursor = Cursors.Hand;
            boton.CheckedChanged += (s, e) =>
            {
                var otro = boton == btnCajaOk ? btnCajaNoOk : btnCajaOk;
                if (boton.Checked) otro.Checked = false;
                PintarEstadoCaja(boton);
                if (!cargando) MarcarCambios();
            };
            PintarEstadoCaja(boton);
            botones.Controls.Add(boton);
        }
        return botones;
    }

    private void PintarEstadoCaja(CheckBox boton)
    {
        var (fondo, texto) = Tema.ColorEstadoCasilla(boton == btnCajaOk ? EstadoCasilla.Correcto : EstadoCasilla.NoCorrecto);
        boton.BackColor = boton.Checked ? fondo : Color.White;
        boton.ForeColor = boton.Checked ? texto : Tema.Texto;
        boton.Font = new Font("Segoe UI", 9.5f, boton.Checked ? FontStyle.Bold : FontStyle.Regular);
        boton.FlatAppearance.BorderColor = boton.Checked ? texto : Tema.Borde;
        boton.FlatAppearance.CheckedBackColor = fondo;
        boton.FlatAppearance.MouseOverBackColor = boton.Checked ? fondo : Tema.AcentoSuave;
    }

    private string? EstadoCaja() => btnCajaOk.Checked ? "OK" : btnCajaNoOk.Checked ? "No OK" : null;

    private void PonerEstadoCaja(string? estado)
    {
        btnCajaOk.Checked = estado == "OK";
        btnCajaNoOk.Checked = estado == "No OK";
    }

    private static void PintarPestana(RadioButton boton)
    {
        boton.BackColor = boton.Checked ? Tema.Acento : Color.White;
        boton.ForeColor = boton.Checked ? Color.White : Tema.Texto;
        boton.Font = new Font("Segoe UI", 9.5f, boton.Checked ? FontStyle.Bold : FontStyle.Regular);
        boton.FlatAppearance.BorderColor = boton.Checked ? Tema.Acento : Tema.Borde;
        boton.FlatAppearance.MouseOverBackColor = boton.Checked ? Tema.Acento : Tema.AcentoSuave;
    }

    // Fila y columna con el ratón encima (-1: ninguna). Se resaltan para seguirlas con la vista.
    private int filaRaton = -1, columnaRaton = -1;

    private void SeguirRaton(int fila, int columna)
    {
        if (fila == filaRaton && columna == columnaRaton) return;
        filaRaton = fila;
        columnaRaton = columna;
        grid.Invalidate();
    }

    private (RutinaPunto Punto, RutinaSeccion Seccion)? Casilla(int fila, int columna)
    {
        if (fila < 0 || columna < 1 || fila >= grid.Rows.Count || columna >= grid.Columns.Count) return null;
        return ((RutinaPunto)grid.Rows[fila].Tag!, (RutinaSeccion)grid.Columns[columna].Tag!);
    }

    // Se puede marcar si la plantilla la revisa o si ya tenía algo (de cuando sí la revisaba)
    private bool Editable(RutinaPunto punto, RutinaSeccion seccion) =>
        plantilla.Aplica.Contains((punto.Id, seccion.Id)) || casillas.ContainsKey((punto.Id, seccion.Id));

    private static string Simbolo(string estado) => estado switch
    {
        EstadoCasilla.Correcto => "✓",
        EstadoCasilla.NoCorrecto => "NO",
        EstadoCasilla.Mantenimiento => "M",
        _ => "N/A"
    };

    // Tecla que marca cada estado (TeclaPulsada); el espacio también vale para "correcto"
    private static readonly Dictionary<Keys, string> Teclas = new()
    {
        [Keys.C] = EstadoCasilla.Correcto,
        [Keys.N] = EstadoCasilla.NoCorrecto,
        [Keys.M] = EstadoCasilla.Mantenimiento,
        [Keys.A] = EstadoCasilla.NoAplica
    };

    private static string Tecla(string estado) => Teclas.First(t => t.Value == estado).Key.ToString();

    private void PintarCasilla(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        // Nombre del punto y cabecera de la máquina bajo el ratón: en morado, para no perderse
        bool filaRaton = e.RowIndex >= 0 && e.RowIndex == this.filaRaton;
        bool columnaRaton = e.ColumnIndex > 0 && e.ColumnIndex == this.columnaRaton;
        if ((e.ColumnIndex == 0 && filaRaton) || (e.RowIndex < 0 && columnaRaton))
        {
            e.CellStyle!.BackColor = e.CellStyle.SelectionBackColor = Tema.AcentoSuave;
            e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Tema.Acento;
        }
        if (e.Graphics == null || Casilla(e.RowIndex, e.ColumnIndex) is not var (punto, seccion)) return;

        var g = e.Graphics;
        var r = e.CellBounds;
        bool seleccionada = (e.State & DataGridViewElementStates.Selected) != 0;
        var casilla = casillas.GetValueOrDefault((punto.Id, seccion.Id));

        Color fondo = Color.White, colorTexto = Tema.Texto;
        string texto = "";
        if (!Editable(punto, seccion))
            fondo = Tema.FondoNoRevisa;
        else if (casilla?.Estado is string estado)
        {
            (fondo, colorTexto) = Tema.ColorEstadoCasilla(estado);
            texto = Simbolo(estado);
        }
        else if (punto.Numerico)
            texto = casilla?.Valor?.ToString("0.##", Componentes.Es) ?? "";
        if (seleccionada && texto == "" && fondo == Color.White)
            fondo = Tema.AcentoSuave;
        else if ((filaRaton || columnaRaton) && fondo == Color.White)
            fondo = Tema.FondoRaton;
        else if ((filaRaton || columnaRaton) && fondo == Tema.FondoNoRevisa)
            fondo = Tema.FondoNoRevisaRaton;

        using (var relleno = new SolidBrush(fondo))
            g.FillRectangle(relleno, r);
        using (var borde = new Pen(Tema.LineaTabla))
        {
            g.DrawLine(borde, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
            g.DrawLine(borde, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
        }

        using (var fuente = new Font("Segoe UI", 9, FontStyle.Bold))
            TextRenderer.DrawText(g, texto, fuente, r, colorTexto,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);

        g.SmoothingMode = SmoothingMode.AntiAlias;
        // Esquina morada si tiene comentario, como en Excel
        if (casilla?.Comentario != null)
        {
            using var esquina = new SolidBrush(Tema.Acento);
            g.FillPolygon(esquina, new[] { new Point(r.Right - 10, r.Top), new Point(r.Right - 1, r.Top), new Point(r.Right - 1, r.Top + 9) });
        }
        // Punto azul si se abrió una incidencia desde la casilla
        if (casilla?.NoConformidadId != null)
        {
            using var marca = new SolidBrush(Color.FromArgb(30, 80, 170));
            g.FillEllipse(marca, r.Right - 11, r.Bottom - 11, 7, 7);
        }
        if (seleccionada)
        {
            using var marco = new Pen(Tema.Acento, 2);
            g.DrawRectangle(marco, r.X + 1, r.Y + 1, r.Width - 3, r.Height - 3);
        }

        e.Handled = true;
    }

    private string TextoAyuda(int fila, int columna)
    {
        if (Casilla(fila, columna) is not var (punto, seccion)) return "";
        if (!Editable(punto, seccion)) return $"{punto.Nombre} no se revisa en {seccion.Nombre}.";

        var casilla = casillas.GetValueOrDefault((punto.Id, seccion.Id));
        var lineas = new List<string> { $"{punto.Nombre} · {seccion.Nombre}" };
        if (casilla?.Estado != null) lineas.Add(EstadoCasilla.Nombre(casilla.Estado));
        if (casilla?.Comentario != null) lineas.Add(casilla.Comentario);
        if (casilla?.NoConformidadId != null)
            lineas.Add($"Incidencia {casilla.NumeroIncidencia ?? "abierta"} (botón derecho para verla)");
        return string.Join("\n", lineas);
    }

    // ---------- Marcar casillas ----------

    private void SeleccionarFila(int fila)
    {
        grid.ClearSelection();
        for (int c = 1; c < grid.Columns.Count; c++)
            grid.Rows[fila].Cells[c].Selected = true;
    }

    private void SeleccionarColumna(int columna)
    {
        grid.EndEdit();
        grid.ClearSelection();
        foreach (DataGridViewRow fila in grid.Rows)
            fila.Cells[columna].Selected = true;
    }

    private List<(RutinaPunto Punto, RutinaSeccion Seccion)> Seleccionadas() =>
        grid.SelectedCells.Cast<DataGridViewCell>()
            .Select(c => Casilla(c.RowIndex, c.ColumnIndex))
            .OfType<(RutinaPunto, RutinaSeccion)>()
            .Where(c => Editable(c.Item1, c.Item2))
            .ToList();

    private void TeclaPulsada(object? sender, KeyEventArgs e)
    {
        if (e.Modifiers != Keys.None) return;   // Ctrl+C y compañía siguen funcionando

        string? estado;
        if (Teclas.TryGetValue(e.KeyCode, out var marcado)) estado = marcado;
        else if (e.KeyCode == Keys.Space) estado = EstadoCasilla.Correcto;
        else if (e.KeyCode is Keys.Delete or Keys.Back) estado = null;
        else return;

        e.SuppressKeyPress = true;   // si no, la letra empezaría a escribir en una casilla de número
        e.Handled = true;
        Marcar(Seleccionadas(), estado);
    }

    // Pone el estado (null = vaciar) en las casillas. A las de número solo se les puede vaciar.
    private void Marcar(IEnumerable<(RutinaPunto Punto, RutinaSeccion Seccion)> lista, string? estado)
    {
        bool cambia = false;
        foreach (var (punto, seccion) in lista)
        {
            if (punto.Numerico && estado != null) continue;

            var clave = (punto.Id, seccion.Id);
            var anterior = casillas.GetValueOrDefault(clave);
            var nueva = (anterior ?? new ResultadoRutina(punto.Id, seccion.Id, null, null, null)) with
            {
                Estado = punto.Numerico ? null : estado,
                Valor = punto.Numerico ? null : anterior?.Valor
            };
            if (nueva == anterior) continue;

            Poner(nueva);
            if (punto.Numerico && ValorDeCelda(punto, seccion) is DataGridViewCell celda) celda.Value = null;
            cambia = true;
        }
        if (!cambia) return;

        grid.Invalidate();
        MarcarCambios();
    }

    // Guarda la casilla en memoria; una que se queda sin nada se quita
    private void Poner(ResultadoRutina casilla)
    {
        var clave = (casilla.PuntoId, casilla.SeccionId);
        if (RutinaServicio.Vacia(casilla) && casilla.NoConformidadId == null)
            casillas.Remove(clave);
        else
            casillas[clave] = casilla;
    }

    private DataGridViewCell? ValorDeCelda(RutinaPunto punto, RutinaSeccion seccion)
    {
        foreach (DataGridViewRow fila in grid.Rows)
            if (fila.Tag is RutinaPunto p && p.Id == punto.Id)
                for (int c = 1; c < grid.Columns.Count; c++)
                    if (grid.Columns[c].Tag is RutinaSeccion s && s.Id == seccion.Id) return fila.Cells[c];
        return null;
    }

    // Las casillas de número (PESOS) se escriben como en una hoja de cálculo
    private void ValidarNumero(object? sender, DataGridViewCellValidatingEventArgs e)
    {
        if (!grid.IsCurrentCellInEditMode || Casilla(e.RowIndex, e.ColumnIndex) is not var (punto, _)) return;
        grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "";
        string texto = e.FormattedValue?.ToString()?.Trim() ?? "";
        if (texto == "") return;

        if (LeerNumero(texto) is not decimal valor
            || RutinaServicio.Error(new ResultadoRutina(punto.Id, 0, null, valor, null), punto) is string)
        {
            grid.Rows[e.RowIndex].Cells[e.ColumnIndex].ErrorText = "Escribe un número, por ejemplo 636 o 12,5.";
            e.Cancel = true;
        }
    }

    private void GuardarNumero(object? sender, DataGridViewCellEventArgs e)
    {
        if (Casilla(e.RowIndex, e.ColumnIndex) is not var (punto, seccion)) return;
        var celda = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        celda.ErrorText = "";

        decimal? valor = LeerNumero(celda.Value?.ToString()?.Trim() ?? "");
        var anterior = casillas.GetValueOrDefault((punto.Id, seccion.Id));
        if (anterior?.Valor == valor) return;

        Poner((anterior ?? new ResultadoRutina(punto.Id, seccion.Id, null, null, null)) with { Valor = valor });
        celda.Value = valor?.ToString("0.##", Componentes.Es);
        MarcarCambios();
    }

    private static decimal? LeerNumero(string texto)
    {
        if (texto == "") return null;
        // "12.5" también vale: en español el punto se tomaría como separador de miles
        if (!texto.Contains(',')) texto = texto.Replace('.', ',');
        return decimal.TryParse(texto, NumberStyles.Number, Componentes.Es, out decimal valor) ? valor : null;
    }

    // ---------- Opciones e incidencias ----------

    private void BotonDerecho(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 1) return;

        var celda = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        if (!celda.Selected)
        {
            grid.ClearSelection();
            celda.Selected = true;
        }
        MostrarOpciones(e.RowIndex, e.ColumnIndex, grid.PointToClient(Cursor.Position));
    }

    // Menú con los estados de la leyenda (con sus colores) para las casillas seleccionadas y,
    // si es una sola, su incidencia. Pulsar el estado que ya tiene la casilla la vacía.
    private void MostrarOpciones(int fila, int columna, Point donde)
    {
        if (Casilla(fila, columna) is not var (punto, seccion) || !Editable(punto, seccion)) return;

        var seleccion = Seleccionadas();
        var casilla = casillas.GetValueOrDefault((punto.Id, seccion.Id));

        var menu = new ContextMenuStrip { Font = Tema.Fuente };
        if (!punto.Numerico)
        {
            foreach (string estado in EstadoCasilla.Todos)
            {
                string elegido = estado;
                var (fondo, texto) = Tema.ColorEstadoCasilla(estado);
                var opcion = new ToolStripMenuItem(
                    $"{Simbolo(estado)}   {EstadoCasilla.Nombre(estado)}   ({Tecla(estado)})", null,
                    (s2, e2) => Marcar(seleccion, seleccion.Count == 1 && casilla?.Estado == elegido ? null : elegido))
                {
                    BackColor = fondo,
                    ForeColor = texto,
                    Font = Tema.FuenteNegrita,
                    Checked = seleccion.Count == 1 && casilla?.Estado == estado
                };
                menu.Items.Add(opcion);
            }
        }
        else
            menu.Items.Add("Vaciar", null, (s2, e2) => Marcar(seleccion, null));

        if (seleccion.Count == 1 && casilla?.NoConformidadId is int ncId)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add($"Ver incidencia {casilla.NumeroIncidencia}", null, (s2, e2) => VerIncidencia(ncId));
        }
        else if (seleccion.Count == 1 && casilla?.Estado == EstadoCasilla.NoCorrecto)
        {
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("Abrir incidencia…", null, (s2, e2) => AbrirIncidencia(punto, seccion));
        }

        menu.Closed += (s2, e2) => BeginInvoke(menu.Dispose);
        menu.Show(grid, donde);
    }

    // La incidencia se apunta en la casilla, así que la ronda tiene que estar guardada antes
    private void AbrirIncidencia(RutinaPunto punto, RutinaSeccion seccion)
    {
        if (hayCambios || rutina.Id == 0)
        {
            if (MessageBox.Show(this, "Para abrir la incidencia hay que guardar antes la ronda. ¿Guardarla ahora?",
                    "Guardar la ronda", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            if (!Guardar()) return;
        }

        var casilla = casillas.GetValueOrDefault((punto.Id, seccion.Id));
        if (casilla?.Estado != EstadoCasilla.NoCorrecto) return;   // otro la cambió al recargar

        int ncId;
        using (var ficha = new FormNoConformidad(null, RutinaServicio.BorradorIncidencia(rutina.Fecha, punto, seccion, casilla)))
        {
            if (ficha.ShowDialog(this) != DialogResult.OK) return;
            ncId = ficha.IdGuardado;
        }

        try
        {
            servicio.Vincular(rutina.Id, punto.Id, seccion.Id, ncId);
        }
        catch (Exception ex) when (ex is DbException or ReglaNegocioException)
        {
            MessageBox.Show(this, "La incidencia se ha guardado, pero no se ha podido apuntar en la casilla.\n\n" + ex.Message,
                "Incidencia sin apuntar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        Cargar(rutina.Fecha, recargarPlantilla: false);
    }

    private void VerIncidencia(int id)
    {
        NoConformidad? nc;
        try
        {
            nc = new NoConformidadServicio().ObtenerRegistro(id);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo cargar la incidencia.", ex);
            return;
        }
        if (nc == null)
        {
            MessageBox.Show(this, "Esta incidencia ya no existe: puede que otro usuario la haya borrado.",
                "Incidencia no encontrada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using (var ficha = new FormNoConformidad(nc))
            ficha.ShowDialog(this);
        // El número puede haber cambiado; lo marcado y sin guardar no se toca
        if (!hayCambios) Cargar(rutina.Fecha, recargarPlantilla: false);
    }

    // ---------- Cargar y guardar ----------

    private void Cargar(DateTime fecha, bool recargarPlantilla)
    {
        try
        {
            if (recargarPlantilla) plantilla = servicio.ObtenerPlantilla();
            var (leida, resultados, leidas) = servicio.ObtenerRutina(fecha);
            rutina = leida;
            casillas = resultados.ToDictionary(r => (r.PuntoId, r.SeccionId));
            observaciones = leidas;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo cargar la rutina de la base de datos.", ex);
            return;
        }

        cargando = true;
        dtpFecha.MaxDate = DateTime.Today;   // por si GPE lleva abierto desde ayer
        dtpFecha.Value = rutina.Fecha;
        cmbCajaOperario.Text = rutina.CajaOperario ?? "";
        PonerEstadoCaja(rutina.CajaEstado);
        txtDepositos.Text = rutina.Depositos ?? "";
        columnaSeccion.DataSource = plantilla.Secciones;
        gridObservaciones.Rows.Clear();
        foreach (var observacion in observaciones)
            gridObservaciones.Rows.Add(observacion.SeccionId, observacion.Texto);
        cargando = false;

        hayCambios = false;
        btnGuardar.Enabled = false;
        Construir();
        ActualizarResumen();
    }

    private void CambiarDia(DateTime dia)
    {
        if (dia == rutina.Fecha) return;
        if (!DescartarCambios())
        {
            cargando = true;
            dtpFecha.Value = rutina.Fecha;
            cargando = false;
            return;
        }
        Cargar(dia, recargarPlantilla: false);
    }

    private bool Guardar()
    {
        grid.EndEdit();
        gridObservaciones.EndEdit();
        try
        {
            var fijas = rutina with { CajaOperario = cmbCajaOperario.Text, CajaEstado = EstadoCaja(), Depositos = txtDepositos.Text };
            servicio.Guardar(fijas, casillas.Values, LeerObservaciones(), plantilla);
        }
        catch (ReglaNegocioException ex)
        {
            MessageBox.Show(this, ex.Message, "No se puede guardar", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return false;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar la ronda.", ex);
            return false;
        }

        // Se vuelve a leer para tener la versión nueva (y lo que haya guardado otro en las casillas)
        Cargar(rutina.Fecha, recargarPlantilla: false);
        return true;
    }

    private void MarcarCambios()
    {
        hayCambios = true;
        btnGuardar.Enabled = true;
        ActualizarResumen();
    }

    private void ActualizarResumen()
    {
        string dia = rutina.Fecha == DateTime.Today ? "hoy" : Componentes.Fecha(rutina.Fecha);
        lblRonda.Text = rutina.Id == 0
            ? $"Ronda de {dia}"
            : $"Ronda de {dia} · {rutina.CreadoPor}";

        lblProgreso.Text = string.Join("  ·  ", new[]
        {
            rutina.Id == 0 ? "Sin empezar" : null,
            hayCambios ? "Cambios sin guardar" : null
        }.OfType<string>());
        lblProgreso.ForeColor = hayCambios ? Tema.Aviso : Tema.Suave;

        int escritas = LeerObservaciones().Count(o => !string.IsNullOrWhiteSpace(o.Texto))
            + (string.IsNullOrWhiteSpace(txtDepositos.Text) ? 0 : 1);
        pestanaObservaciones.Text = "Observaciones" + (escritas > 0 ? $"  ·  {escritas}" : "");
    }

    private bool DescartarCambios() =>
        !hayCambios || MessageBox.Show(this, "Hay cambios sin guardar en la ronda. ¿Descartarlos?",
            "Cambios sin guardar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private void AvisarAlCerrar(object? sender, FormClosingEventArgs e)
    {
        if (!e.Cancel && !DescartarCambios()) e.Cancel = true;
    }

    private void ConfigurarPlantilla()
    {
        if (!DescartarCambios()) return;
        using (var formulario = new FormPlantillaRutina())
            if (formulario.ShowDialog(this) != DialogResult.OK) return;
        Cargar(rutina.Fecha, recargarPlantilla: true);
    }
}
