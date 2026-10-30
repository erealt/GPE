using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Drawing.Drawing2D;
using System.Globalization;

namespace GPE.Escritorio;

// Rutina diaria del supervisor de calidad (la hoja "CHEQUEOS DIARIOS - PCC")
public class ModuloRutinaCalidad : UserControl
{
    private const int AnchoPunto = 220;
    private const int AnchoSeccion = 58;
    public static int AnchoColumna(string nombre) =>
        Math.Max(AnchoSeccion, nombre.Split(' ').Max(palabra => TextRenderer.MeasureText(palabra, Tema.FuenteEtiqueta).Width) + 26);
    private static int AnchoColumnaCompacta(string nombre) =>
        Math.Max(AnchoSeccionMinimo, nombre.Split(' ').Max(palabra => TextRenderer.MeasureText(palabra, Tema.FuenteEtiqueta).Width) + 12);

    private readonly RutinaServicio servicio = new();
    private PlantillaRutina plantilla = new(new(), new(), new());
    private Rutina rutina = new(0, DateTime.Today, null, null, null, null);
    private List<RevisionRutina> revisiones = new() { new(1, null) };
    private int revisionActual = 1;

    private Dictionary<int, Dictionary<(int PuntoId, int SeccionId), ResultadoRutina>> porRevision = new();
    private List<ObservacionRutina> observaciones = new();
    private bool hayCambios;
    private bool cargando;

    private readonly DateTimePicker dtpFecha = new();
    private string plantaElegida = Planta.Baja;
    private readonly Label lblRonda = new();
    private readonly Label lblProgreso = new();
    private readonly DataGridView grid = new();
    private readonly Button btnGuardar = Tema.BotonPrincipal("Guardar ronda", 170);
    private readonly FlowLayoutPanel pestanasRevision = new();
    private readonly Button btnNuevaRevision = Tema.BotonSecundario("+ Nueva revisión", 160);
    private readonly ToolTip ayuda = new();

    private static readonly string[] Plantas = { Planta.Baja, Planta.Superior };

    private readonly RadioButton pestanaObservaciones = new();
    private Control panelObservaciones = null!;

    private readonly Button btnCajaOperario = Tema.BotonSecundario("", 300);
    private string? cajaOperario;
    private List<string> operarios = new();
    private readonly CheckBox btnCajaOk = new(), btnCajaNoOk = new();  
    private readonly TextBox txtDepositos = new();
    private readonly DataGridView gridObservaciones = new();
    private const int ColObservacionSeccion = 0, ColObservacionTexto = 1, ColObservacionQuitar = 2;

    public ModuloRutinaCalidad()
    {
        Dock = DockStyle.Fill;
        Controls.Add(CrearContenido());
        Load += (s, e) =>
        {
            CargarOperarios();
            Cargar(DateTime.Today, recargarPlantilla: true);
            
            if (FindForm() is Form ventana) ventana.FormClosing += AvisarAlCerrar;
        };
    }

    private Control CrearContenido()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(40, 30, 40, 20)
        };
        foreach (int alto in AltosDeArriba)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, alto));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // La tabla y las observaciones ocupan el mismo sitio: se ve una u otra según la pestaña
        var zona = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        panelObservaciones = CrearObservaciones();
        panelObservaciones.Visible = false;
        zona.Controls.Add(panelObservaciones);
        zona.Controls.Add(ConfigurarGrid());

        layout.Controls.Add(CrearCabecera(), 0, 0);
        layout.Controls.Add(CrearBarra(), 0, 1);
        layout.Controls.Add(CrearPestanasRevision(), 0, 2);
        layout.Controls.Add(CrearLeyenda(), 0, 3);
        layout.Controls.Add(CrearPestanasPlanta(), 0, 4);
        layout.Controls.Add(zona, 0, 5);
        this.layout = layout;
        return layout;
    }

    private TableLayoutPanel layout = null!;
    private bool pantallaCompleta;
    private readonly Button btnPantallaCompleta = Tema.BotonSecundario("⛶  Pantalla completa", 190);
    private readonly Button btnGuardarCompacto = Tema.BotonPrincipal("Guardar", 120);
    private readonly Label lblRondaCompacta = new();
    private const int AltoFilaNormal = 32, AltoFilaMinimo = 14, AltoFilaComoda = 20, AltoFilaMaximo = 48;
    private float escalaAlto = 1f;
    private const int AnchoSeccionMaximo = 110, AnchoSeccionMinimo = 34;
    private const int AltoCabecera = 76, AltoCabeceraCompacta = 52;
    private const int FilasDeArriba = 4;   // cabecera, día, revisiones y leyenda
    private static readonly int[] AltosDeArriba = { 95, 80, 56, 68 };

    private void PantallaCompleta(bool activar)
    {
        if (activar == pantallaCompleta || FindForm() is not FormPrincipal ventana) return;
        pantallaCompleta = activar;

        layout.SuspendLayout();
        for (int i = 0; i < FilasDeArriba; i++)
        {
            layout.GetControlFromPosition(0, i)!.Visible = !activar;
            layout.RowStyles[i].Height = activar ? 0 : AltosDeArriba[i];   // una fila oculta sigue ocupando su alto
        }
        layout.Padding = activar ? new Padding(20, 12, 20, 12) : new Padding(40, 30, 40, 20);
        lblRondaCompacta.Text = $"{Componentes.Fecha(rutina.Fecha)}  ·  {revisionActual}ª revisión";
        lblRondaCompacta.Visible = btnGuardarCompacto.Visible = activar;
        btnPantallaCompleta.Text = activar ? "Salir (Esc)" : "⛶  Pantalla completa";
        grid.ColumnHeadersHeight = activar ? AltoCabeceraCompacta : AltoCabecera;
        layout.ResumeLayout();

        ventana.PantallaCompleta(activar);
        AjustarAltoFilas();
        grid.Focus();
    }

    private void AjustarAltoFilas()
    {
        AjustarAnchoColumnas();

        int alto = AltoFilaNormal;
        if (pantallaCompleta && grid.Rows.Count > 0)
        {
            int disponible = grid.ClientSize.Height - grid.ColumnHeadersHeight - 2;
            if (grid.Controls.OfType<HScrollBar>().Any(b => b.Visible))
                disponible -= SystemInformation.HorizontalScrollBarHeight;
            alto = Math.Clamp(disponible / grid.Rows.Count, AltoFilaMinimo, AltoFilaMaximo);
        }
        // Filas más bajas de lo normal (pantalla pequeña) piden letra más pequeña
        escalaAlto = Math.Min(1f, (float)alto / AltoFilaComoda);

        grid.RowTemplate.Height = alto;
        foreach (DataGridViewRow fila in grid.Rows)
            if (fila.Height != alto) fila.Height = alto;

        // Al encoger las filas puede desaparecer la barra vertical y quedar sitio para las columnas
        if (pantallaCompleta) AjustarAnchoColumnas();
    }

    private void AjustarAnchoColumnas()
    {
        var columnas = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Tag != null).ToList();
        if (columnas.Count == 0) return;

        if (!pantallaCompleta)
        {
            PonerEscala(1f);
            grid.Columns[0].Width = AnchoPunto;
            foreach (var columna in columnas)
                if (columna.Width != AnchoColumna(columna.HeaderText)) columna.Width = AnchoColumna(columna.HeaderText);
            return;
        }

        int disponible = grid.ClientSize.Width - 2;
        if (grid.Controls.OfType<VScrollBar>().Any(b => b.Visible))
            disponible -= SystemInformation.VerticalScrollBarWidth;

        // Si ni con lo mínimo cabe (pantalla pequeña), se encoge todo por igual, letra incluida
        var minimos = columnas.Select(c => AnchoColumnaCompacta(c.HeaderText)).ToList();
        float escala = Math.Min(1f, (float)disponible / (AnchoPunto + minimos.Sum()));
        PonerEscala(Math.Min(escala, escalaAlto));
        int anchoPunto = (int)(AnchoPunto * escala);
        grid.Columns[0].Width = anchoPunto;

        // El sobrante se reparte a partes iguales; las que necesitan más se quedan con su mínimo
        var anchos = minimos.Select(m => (int)(m * escala)).ToList();
        var libres = Enumerable.Range(0, columnas.Count).ToList();
        int resto = disponible - anchoPunto;
        while (libres.Count > 0)
        {
            int parte = Math.Min(resto / libres.Count, AnchoSeccionMaximo);
            var fijas = libres.Where(i => anchos[i] > parte).ToList();
            if (fijas.Count == 0)
            {
                foreach (int i in libres) anchos[i] = parte;
                break;
            }
            foreach (int i in fijas) { resto -= anchos[i]; libres.Remove(i); }
        }

        for (int i = 0; i < columnas.Count; i++)
            if (columnas[i].Width != anchos[i]) columnas[i].Width = anchos[i];
    }

    // Letra de la cabecera y de los puntos a la escala de la tabla (1 = tamaño normal)
    private float escalaActual = 1f;
    private Font? fuenteCabeceraEscalada, fuentePuntoEscalada;

    private void PonerEscala(float escala)
    {
        escala = (float)Math.Round(escala, 2);
        if (escala == escalaActual) return;
        escalaActual = escala;

        var anteriores = (fuenteCabeceraEscalada, fuentePuntoEscalada);
        fuenteCabeceraEscalada = escala < 1f ? Escalar(Tema.FuenteEtiqueta, escala) : null;
        fuentePuntoEscalada = escala < 1f ? Escalar(Tema.FuentePequenaNegrita, escala) : null;
        grid.ColumnHeadersDefaultCellStyle.Font = fuenteCabeceraEscalada ?? Tema.FuenteEtiqueta;
        grid.Columns[0].DefaultCellStyle.Font = fuentePuntoEscalada ?? Tema.FuentePequenaNegrita;
        anteriores.fuenteCabeceraEscalada?.Dispose();
        anteriores.fuentePuntoEscalada?.Dispose();
    }

    private static Font Escalar(Font fuente, float escala) =>
        new(fuente.FontFamily, Math.Max(6f, fuente.Size * escala), fuente.Style);

    private Font FuenteCasilla => fuentePuntoEscalada ?? Tema.FuentePequenaNegrita;

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.F11 || (keyData == Keys.Escape && pantallaCompleta && !grid.IsCurrentCellInEditMode))
        {
            PantallaCompleta(!pantallaCompleta);
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }
    private Control CrearPestanasRevision()
    {
        pestanasRevision.Dock = DockStyle.Fill;
        pestanasRevision.WrapContents = false;
        pestanasRevision.Padding = new Padding(0, 8, 0, 0);
        btnNuevaRevision.Height = 40;
        btnNuevaRevision.Margin = new Padding(10, 0, 0, 0);
        btnNuevaRevision.Click += (s, e) => NuevaRevision();
        ayuda.SetToolTip(btnNuevaRevision, "Empieza otra revisión ahora, con la plantilla en blanco.");
        return pestanasRevision;
    }

    // Se rehacen al cargar el día y al cambiar las revisiones o su hora
    private void MostrarPestanasRevision()
    {
        pestanasRevision.SuspendLayout();
        foreach (var control in pestanasRevision.Controls.Cast<Control>().Where(c => c != btnNuevaRevision).ToList())
            control.Dispose();
        pestanasRevision.Controls.Clear();

        pestanasRevision.Controls.Add(new Label
        {
            Text = "Revisión",
            AutoSize = true,
            ForeColor = Tema.Suave,
            Margin = new Padding(0, 11, 10, 0)
        });
        foreach (var revision in revisiones)
        {
            int numero = revision.Numero;
            var boton = new RadioButton
            {
                Text = RutinaServicio.NombreRevision(revision),
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(190, 40),
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                Checked = numero == revisionActual
            };
            ayuda.SetToolTip(boton, revision.CreadoPor != null
                ? $"Empezada a las {revision.Hora:hh\\:mm} por {revision.CreadoPor}"
                : "Sin guardar");
            boton.CheckedChanged += (s, e) =>
            {
                PintarRevision(boton);
                if (boton.Checked) CambiarRevision(numero);
            };
            PintarRevision(boton);
            pestanasRevision.Controls.Add(boton);
        }
        pestanasRevision.Controls.Add(btnNuevaRevision);
        pestanasRevision.ResumeLayout();
    }

    // Más discretas que las de planta, que van debajo
    private static void PintarRevision(RadioButton boton)
    {
        boton.BackColor = boton.Checked ? Tema.AcentoSuave : Color.White;
        boton.ForeColor = boton.Checked ? Tema.Acento : Tema.Texto;
        boton.Font = Tema.FuenteDeOpcion(boton.Checked);
        boton.FlatAppearance.BorderColor = boton.Checked ? Tema.Acento : Tema.Borde;
        boton.FlatAppearance.CheckedBackColor = Tema.AcentoSuave;
        boton.FlatAppearance.MouseOverBackColor = Tema.AcentoSuave;
    }

    private void CambiarRevision(int numero)
    {
        if (numero == revisionActual) return;
        grid.EndEdit();
        revisionActual = numero;
        Construir();
        ActualizarResumen();
    }

    // La nueva empieza ahora y en blanco. Si no se marca nada en ella, no se guarda.
    private void NuevaRevision()
    {
        grid.EndEdit();
        int numero = revisiones.Max(r => r.Numero) + 1;
        revisiones.Add(new RevisionRutina(numero, DateTime.Now.TimeOfDay));
        revisionActual = numero;
        MostrarPestanasRevision();
        Construir();
        ActualizarResumen();
    }

    // Casillas de la revisión que se está viendo
    private Dictionary<(int PuntoId, int SeccionId), ResultadoRutina> casillas
    {
        get
        {
            if (!porRevision.TryGetValue(revisionActual, out var lista))
                porRevision[revisionActual] = lista = new();
            return lista;
        }
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
            var boton = Tema.BotonSecundario(texto, 48);
            boton.Height = 40;
            boton.Margin = new Padding(0, 18, 6, 0);
            boton.Click += (s, e) =>
            {
                var dia = dtpFecha.Value.Date.AddDays(dias);
                if (dia <= dtpFecha.MaxDate) CambiarDia(dia);
            };
            return boton;
        }
        var btnHoy = Tema.BotonSecundario("Hoy", 70);
        btnHoy.Height = 40;
        btnHoy.Margin = new Padding(0, 18, 20, 0);
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
        btnRecargar.Height = 40;
        btnRecargar.Click += (s, e) =>
        {
            if (DescartarCambios()) Cargar(rutina.Fecha, recargarPlantilla: true);
        };
        var btnPlantilla = Tema.BotonSecundario("Plantilla", 110);
        btnPlantilla.Height = 40;
        btnPlantilla.Margin = new Padding(10, 3, 0, 3);
        btnPlantilla.Click += (s, e) => ConfigurarPlantilla();

        var botones = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 12, 0, 0) };
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
            Text = "Pulsa una casilla para marcarla. Pulsa el nombre de un punto o de una máquina " +
                   "para marcarla entera (\"Todo correcto\"). En PESOS se apunta el número con el teclado que sale.",
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

        btnCajaOperario.Height = 48;
        btnCajaOperario.TextAlign = ContentAlignment.MiddleLeft;
        btnCajaOperario.Padding = new Padding(10, 0, 0, 0);
        btnCajaOperario.Click += (s, e) => ElegirOperario();
        MostrarOperario();

        var caja = Componentes.Tarjeta("Caja de herramientas", "Quién la tiene hoy y cómo está.");
        var rejilla = Componentes.Rejilla(2);
        rejilla.Controls.Add(campos.Campo("Operario", btnCajaOperario));
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

        var btnAnadirFila = Tema.BotonSecundario("+  Añadir observación", 220);
        btnAnadirFila.Height = 48;
        btnAnadirFila.Click += (s, e) => AnadirFilaObservacion(btnAnadirFila);
        var secciones = Componentes.Tarjeta("Observaciones por sección",
            "Pulsa \"+ Añadir observación\", elige la sección y escribe. Pulsa la sección para cambiarla y ✕ para quitarla.",
            btnAnadirFila);
        secciones.Controls.Add(ConfigurarGridObservaciones());
        pila.Controls.Add(secciones);

        return scroll;
    }

    private Control ConfigurarGridObservaciones()
    {
        Componentes.EstiloTabla(gridObservaciones, altoFila: 48);
        gridObservaciones.Dock = DockStyle.Top;
        gridObservaciones.ScrollBars = ScrollBars.None;   // crece con las filas; desplaza la página
        gridObservaciones.AllowUserToDeleteRows = true;
        gridObservaciones.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        gridObservaciones.GridColor = Tema.LineaTabla;
        gridObservaciones.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        gridObservaciones.MultiSelect = false;

        gridObservaciones.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "SECCIÓN",
            Width = 220,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        gridObservaciones.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "OBSERVACIÓN",
            MaxInputLength = RutinaServicio.LargoComentario,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });
        gridObservaciones.Columns.Add(new DataGridViewTextBoxColumn
        {
            HeaderText = "",
            Width = 56,
            AutoSizeMode = DataGridViewAutoSizeColumnMode.None,
            ReadOnly = true,
            ToolTipText = "Quitar la observación",
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        gridObservaciones.CellClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            var fila = gridObservaciones.Rows[e.RowIndex];
            if (e.ColumnIndex == ColObservacionSeccion) ElegirSeccionObservacion(fila);
            else if (e.ColumnIndex == ColObservacionTexto) gridObservaciones.BeginEdit(true);
            else if (e.ColumnIndex == ColObservacionQuitar) QuitarObservacion(fila);
        };
        gridObservaciones.KeyDown += (s, e) =>
        {
            if (e.KeyCode is not (Keys.Enter or Keys.F2) || gridObservaciones.IsCurrentCellInEditMode
                || gridObservaciones.CurrentCell is not { ColumnIndex: ColObservacionSeccion } celda) return;
            ElegirSeccionObservacion(gridObservaciones.Rows[celda.RowIndex]);
            e.Handled = e.SuppressKeyPress = true;
        };
        // La celda guarda el id de la sección: se enseña su nombre
        gridObservaciones.CellFormatting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != ColObservacionSeccion) return;
            e.Value = NombreSeccion(gridObservaciones.Rows[e.RowIndex]) ?? "—";
            e.FormattingApplied = true;
        };
        gridObservaciones.CellPainting += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex != ColObservacionQuitar || e.Graphics == null) return;
            e.PaintBackground(e.CellBounds, false);
            TextRenderer.DrawText(e.Graphics, "✕", Tema.FuenteNegrita, e.CellBounds, Tema.Suave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            e.Handled = true;
        };
        gridObservaciones.CellValueChanged += (s, e) => { if (!cargando) MarcarCambios(); };
        gridObservaciones.UserDeletedRow += (s, e) => MarcarCambios();
        gridObservaciones.RowsAdded += (s, e) => AjustarAltoObservaciones();
        gridObservaciones.RowsRemoved += (s, e) => AjustarAltoObservaciones();
        AjustarAltoObservaciones();
        return gridObservaciones;
    }

    private string? NombreSeccion(DataGridViewRow fila) =>
        fila.Cells[ColObservacionSeccion].Value is int id ? plantilla.Secciones.FirstOrDefault(s => s.Id == id)?.Nombre : null;

    private ElegirOpcion PanelSecciones(DataGridViewRow? fila) =>
        new("Sección", plantilla.Secciones.Select(s => (s.Id.ToString(), s.Nombre, true)).ToList(),
            (fila?.Cells[ColObservacionSeccion].Value as int?)?.ToString());

    private void AnadirFilaObservacion(Control boton)
    {
        gridObservaciones.EndEdit();
        var panel = PanelSecciones(null);
        panel.Abrir(this, boton.PointToScreen(new Point(0, boton.Height)), () =>
        {
            if (panel.Elegida is not string clave) return;
            int i = gridObservaciones.Rows.Add(int.Parse(clave), "", "");
            gridObservaciones.Focus();
            gridObservaciones.CurrentCell = gridObservaciones.Rows[i].Cells[ColObservacionTexto];
            gridObservaciones.BeginEdit(true);
        });
    }

    private void ElegirSeccionObservacion(DataGridViewRow fila)
    {
        var panel = PanelSecciones(fila);
        var celda = gridObservaciones.GetCellDisplayRectangle(ColObservacionSeccion, fila.Index, false);
        panel.Abrir(this, gridObservaciones.PointToScreen(new Point(celda.Left, celda.Bottom)), () =>
        {
            if (fila.DataGridView != null && panel.Elegida is string clave
                && !Equals(fila.Cells[ColObservacionSeccion].Value, int.Parse(clave)))
                fila.Cells[ColObservacionSeccion].Value = int.Parse(clave);
        });
    }

    private void QuitarObservacion(DataGridViewRow fila)
    {
        if (!Mensajes.Confirmar(this, "Quitar observación", $"¿Quitar la observación de {NombreSeccion(fila) ?? "esta fila"}?"))
            return;
        gridObservaciones.Rows.Remove(fila);
        MarcarCambios();
    }

    private void ElegirOperario()
    {
        var nombres = operarios.ToList();
        if (cajaOperario != null && !nombres.Contains(cajaOperario)) nombres.Insert(0, cajaOperario);
        var panel = new ElegirOpcion("¿Quién tiene la caja de herramientas?",
            nombres.Select(n => (n, n, true)).ToList(), cajaOperario, textoQuitar: "Quitar el operario");
        panel.Abrir(this, btnCajaOperario.PointToScreen(new Point(0, btnCajaOperario.Height)), () =>
        {
            string? elegido = panel.Quitar ? null : panel.Elegida ?? cajaOperario;
            if (elegido == cajaOperario) return;
            cajaOperario = elegido;
            MostrarOperario();
            MarcarCambios();
        });
    }

    private void MostrarOperario()
    {
        btnCajaOperario.Text = cajaOperario ?? "Elige quién la tiene…";
        btnCajaOperario.ForeColor = cajaOperario == null ? Tema.Suave : Tema.Texto;
    }

    // Siempre se ven todas las filas, y como mínimo hueco para cinco
    private void AjustarAltoObservaciones()
    {
        int filas = Math.Max(gridObservaciones.Rows.Count, 5);
        gridObservaciones.Height = gridObservaciones.ColumnHeadersHeight
            + filas * gridObservaciones.RowTemplate.Height + 2;
    }

    private void CargarOperarios()
    {
        try
        {
            var lista = new NoConformidadServicio().ObtenerListas().GetValueOrDefault(ListaCalidad.Operario) ?? new();
            operarios = lista.Select(o => o.Valor).ToList();
        }
        catch (DbException)
        {
        
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

    // Tabla

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
        grid.ColumnHeadersHeight = AltoCabecera;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;

        grid.CellPainting += PintarCasilla;
        grid.KeyDown += TeclaPulsada;
        grid.CellMouseDown += BotonDerecho;
      
        grid.CellMouseClick += (s, e) =>
        {
            if (e.Button != MouseButtons.Left || ModifierKeys != Keys.None || grid.SelectedCells.Count != 1
                || Casilla(e.RowIndex, e.ColumnIndex) is not var (punto, seccion) || !Editable(punto, seccion)) return;

            var celda = grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            var debajo = new Point(celda.Left, celda.Bottom);
            if (punto.Numerico)
                PedirNumero(punto, seccion, grid.Rows[e.RowIndex].Cells[e.ColumnIndex], debajo);
            else
                MostrarOpciones(debajo);
        };
      
        grid.CellClick += (s, e) =>
        {
            if (e.ColumnIndex != 0 || e.RowIndex < 0) return;
            SeleccionarFila(e.RowIndex);
            var celda = grid.GetCellDisplayRectangle(0, e.RowIndex, false);
            MostrarOpciones(new Point(celda.Right, celda.Top));
        };
        grid.ColumnHeaderMouseClick += (s, e) =>
        {
            if (e.ColumnIndex < 1) return;
            SeleccionarColumna(e.ColumnIndex);
            var cabecera = grid.GetCellDisplayRectangle(e.ColumnIndex, -1, false);
            MostrarOpciones(new Point(cabecera.Left, cabecera.Bottom));
        };
        grid.CellToolTipTextNeeded += (s, e) => e.ToolTipText = TextoAyuda(e.RowIndex, e.ColumnIndex);
        grid.CellValidating += ValidarNumero;
        grid.CellEndEdit += GuardarNumero;
        grid.Resize += (s, e) => { if (pantallaCompleta) AjustarAltoFilas(); };

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
        columnaPunto.DefaultCellStyle.Font = FuenteCasilla;
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
        AjustarAltoFilas();
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
                Size = new Size(260, 44),
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
        pestanaObservaciones.Size = new Size(200, 44);
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

        // A la derecha: pantalla completa y, dentro de ella, el día y "Guardar" (el de arriba no se ve)
        var derecha = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            WrapContents = false,
            Padding = new Padding(0, 6, 0, 0)
        };
        lblRondaCompacta.AutoSize = true;
        lblRondaCompacta.ForeColor = Tema.Suave;
        lblRondaCompacta.Margin = new Padding(0, 13, 14, 0);
        lblRondaCompacta.Visible = false;
        btnGuardarCompacto.Height = 44;
        btnGuardarCompacto.Margin = new Padding(0, 0, 8, 0);
        btnGuardarCompacto.Visible = false;
        btnGuardarCompacto.Enabled = btnGuardar.Enabled;
        btnGuardar.EnabledChanged += (s, e) => btnGuardarCompacto.Enabled = btnGuardar.Enabled;
        btnGuardarCompacto.Click += (s, e) => Guardar();
        btnPantallaCompleta.Height = 44;
        btnPantallaCompleta.Margin = new Padding(0);
        btnPantallaCompleta.Click += (s, e) => PantallaCompleta(!pantallaCompleta);
        ayuda.SetToolTip(btnPantallaCompleta, "Ver la tabla sola, con todas las filas a la vista (F11)");
        derecha.Controls.Add(lblRondaCompacta);
        derecha.Controls.Add(btnGuardarCompacto);
        derecha.Controls.Add(btnPantallaCompleta);

        var fila = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
        fila.Controls.Add(pestanas);
        fila.Controls.Add(derecha);
        return fila;
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
            boton.Size = new Size(150, 48);
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
        boton.Font = Tema.FuenteDeOpcion(boton.Checked);
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
        boton.Font = Tema.FuenteDeOpcion(boton.Checked);
        boton.FlatAppearance.BorderColor = boton.Checked ? Tema.Acento : Tema.Borde;
        boton.FlatAppearance.MouseOverBackColor = boton.Checked ? Tema.Acento : Tema.AcentoSuave;
    }

    private (RutinaPunto Punto, RutinaSeccion Seccion)? Casilla(int fila, int columna)
    {
        if (fila < 0 || columna < 1 || fila >= grid.Rows.Count || columna >= grid.Columns.Count) return null;
        return ((RutinaPunto)grid.Rows[fila].Tag!, (RutinaSeccion)grid.Columns[columna].Tag!);
    }

    // Se puede marcar si la plantilla la revisa o si ya tenía algo (de cuando sí la revisaba)
    private bool Editable(RutinaPunto punto, RutinaSeccion seccion) =>
        plantilla.Aplica.Contains((punto.Id, seccion.Id)) || casillas.ContainsKey((punto.Id, seccion.Id));

    private static string Simbolo(string estado) => OpcionesCasilla.Simbolo(estado);

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

        using (var relleno = new SolidBrush(fondo))
            g.FillRectangle(relleno, r);
        using (var borde = new Pen(Tema.LineaTabla))
        {
            g.DrawLine(borde, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
            g.DrawLine(borde, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
        }

        TextRenderer.DrawText(g, texto, FuenteCasilla, r, colorTexto,
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
            lineas.Add($"Incidencia {casilla.NumeroIncidencia ?? "abierta"} (pulsa la casilla para verla)");
        return string.Join("\n", lineas);
    }

    // Marcar casillas

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

        e.SuppressKeyPress = true;   
        e.Handled = true;
        Marcar(Seleccionadas(), estado);
    }

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

    private void Poner(ResultadoRutina casilla)
    {
        casilla = casilla with { Revision = revisionActual };
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
    private void PedirNumero(RutinaPunto punto, RutinaSeccion seccion, DataGridViewCell celda, Point donde)
    {
        string actual = casillas.GetValueOrDefault((punto.Id, seccion.Id))?.Valor?.ToString("0.##", Componentes.Es) ?? "";
        var panel = new TecladoNumerico($"{punto.Nombre} · {seccion.Nombre}", actual, largoMaximo: 10);
        panel.Abrir(this, grid.PointToScreen(donde), () =>
        {
            if (panel.Valor is not string texto || texto == actual || celda.DataGridView == null) return;

            decimal? valor = LeerNumero(texto);
            if (texto != "" && (valor == null
                || RutinaServicio.Error(new ResultadoRutina(punto.Id, 0, null, valor, null), punto) is string))
            {
                Mensajes.Aviso(this, "Número no válido", "Escribe un número, por ejemplo 636 o 12,5.");
                return;
            }

            var anterior = casillas.GetValueOrDefault((punto.Id, seccion.Id));
            Poner((anterior ?? new ResultadoRutina(punto.Id, seccion.Id, null, null, null)) with { Valor = valor });
            celda.Value = valor?.ToString("0.##", Componentes.Es);
            MarcarCambios();
        });
    }

    private static decimal? LeerNumero(string texto)
    {
        if (texto == "") return null;
        // "12.5" también vale: en español el punto se tomaría como separador de miles
        if (!texto.Contains(',')) texto = texto.Replace('.', ',');
        return decimal.TryParse(texto, NumberStyles.Number, Componentes.Es, out decimal valor) ? valor : null;
    }

    private void BotonDerecho(object? sender, DataGridViewCellMouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right || e.RowIndex < 0 || e.ColumnIndex < 1) return;

        var celda = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        if (!celda.Selected)
        {
            grid.ClearSelection();
            celda.Selected = true;
        }
        MostrarOpciones(grid.PointToClient(Cursor.Position));
    }

    private void MostrarOpciones(Point donde)
    {
        var seleccion = Seleccionadas();
        if (seleccion.Count == 0) return;

        var (punto, seccion) = seleccion[0];
        bool una = seleccion.Count == 1;
        var casilla = una ? casillas.GetValueOrDefault((punto.Id, seccion.Id)) : null;
        bool numerico = seleccion.All(c => c.Punto.Numerico);

        // "PESOS · ENVASADORA 1", "ORDEN Y LIMPIEZA · 12 casillas", "ENVASADORA 1 · 25 casillas"...
        string titulo = una ? $"{punto.Nombre} · {seccion.Nombre}"
            : seleccion.All(c => c.Punto.Id == punto.Id) ? $"{punto.Nombre} · {seleccion.Count} casillas"
            : seleccion.All(c => c.Seccion.Id == seccion.Id) ? $"{seccion.Nombre} · {seleccion.Count} casillas"
            : $"{seleccion.Count} casillas";
        string? incidencia = !una ? null
            : casilla?.NoConformidadId != null ? $"Ver incidencia {casilla.NumeroIncidencia}"
            : casilla?.Estado == EstadoCasilla.NoCorrecto ? "Abrir incidencia…"
            : null;

        var panel = new OpcionesCasilla(titulo, seleccion.Count, numerico, casilla?.Estado, incidencia);
        panel.Abrir(this, grid.PointToScreen(donde), () =>
        {
            if (panel.EligioEstado)
                Marcar(panel.SoloVacias ? seleccion.Where(c => casillas.GetValueOrDefault((c.Punto.Id, c.Seccion.Id))?.Estado == null) : seleccion,
                    panel.Estado);
            if (panel.PideIncidencia)
            {
                // Se abre cuando el panel ya se ha cerrado del todo
                if (casilla?.NoConformidadId is int ncId) BeginInvoke(() => VerIncidencia(ncId));
                else BeginInvoke(() => AbrirIncidencia(punto, seccion));
            }
        });
    }

    private void AbrirIncidencia(RutinaPunto punto, RutinaSeccion seccion)
    {
        if (hayCambios || rutina.Id == 0)
        {
            if (!Mensajes.Confirmar(this, "Guardar la ronda", "Para abrir la incidencia hay que guardar antes la ronda. ¿Guardarla ahora?"))
                return;
            if (!Guardar()) return;
        }

        var casilla = casillas.GetValueOrDefault((punto.Id, seccion.Id));
        if (casilla?.Estado != EstadoCasilla.NoCorrecto) return;  

        int ncId;
        var revision = revisiones.First(r => r.Numero == revisionActual);
        using (var ficha = new FormNoConformidad(null,
            RutinaServicio.BorradorIncidencia(rutina.Fecha, revision, punto, seccion, casilla)))
        {
            if (ficha.ShowDialog(this) != DialogResult.OK) return;
            ncId = ficha.IdGuardado;
        }

        try
        {
            servicio.Vincular(rutina.Id, revisionActual, punto.Id, seccion.Id, ncId);
        }
        catch (Exception ex) when (ex is DbException or ReglaNegocioException)
        {
            Mensajes.Aviso(this, "Incidencia sin apuntar", "La incidencia se ha guardado, pero no se ha podido apuntar en la casilla.\n\n" + ex.Message);
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
            Mensajes.Informacion(this, "Incidencia no encontrada", "Esta incidencia ya no existe: puede que otro usuario la haya borrado.");
            return;
        }

        using (var ficha = new FormNoConformidad(nc))
            ficha.ShowDialog(this);
        // El número puede haber cambiado; lo marcado y sin guardar no se toca
        if (!hayCambios) Cargar(rutina.Fecha, recargarPlantilla: false);
    }

    // Cargar y guardar

    private void Cargar(DateTime fecha, bool recargarPlantilla)
    {
        try
        {
            if (recargarPlantilla) plantilla = servicio.ObtenerPlantilla();
            var (leida, leidasRevisiones, resultados, leidas) = servicio.ObtenerRutina(fecha);
            bool mismoDia = leida.Fecha == rutina.Fecha;
            rutina = leida;
            revisiones = leidasRevisiones;
            if (!mismoDia) revisionActual = 1;
            else if (!revisiones.Any(r => r.Numero == revisionActual)) revisionActual = revisiones.Max(r => r.Numero);
            porRevision = resultados.GroupBy(r => r.Revision)
                .ToDictionary(g => g.Key, g => g.ToDictionary(r => (r.PuntoId, r.SeccionId)));
            observaciones = leidas;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo cargar la rutina de la base de datos.", ex);
            return;
        }

        cargando = true;
        dtpFecha.MaxDate = DateTime.Today;   
        dtpFecha.Value = rutina.Fecha;
        cajaOperario = string.IsNullOrWhiteSpace(rutina.CajaOperario) ? null : rutina.CajaOperario;
        MostrarOperario();
        PonerEstadoCaja(rutina.CajaEstado);
        txtDepositos.Text = rutina.Depositos ?? "";
        gridObservaciones.Rows.Clear();
        foreach (var observacion in observaciones)
            gridObservaciones.Rows.Add(observacion.SeccionId, observacion.Texto, "");
        cargando = false;

        hayCambios = false;
        btnGuardar.Enabled = false;
        MostrarPestanasRevision();
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
            var fijas = rutina with { CajaOperario = cajaOperario, CajaEstado = EstadoCaja(), Depositos = txtDepositos.Text };
            servicio.Guardar(fijas, revisiones, porRevision.Values.SelectMany(c => c.Values), LeerObservaciones(), plantilla);
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Aviso(this, "No se puede guardar", ex.Message);
            return false;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar la ronda.", ex);
            return false;
        }

        Cargar(rutina.Fecha, recargarPlantilla: false);
        return true;
    }

    private void MarcarCambios()
    {
        hayCambios = true;
        btnGuardar.Enabled = true;

        int sinHora = revisiones.FindIndex(r => r.Hora == null);
        if (sinHora >= 0)
        {
            revisiones[sinHora] = revisiones[sinHora] with { Hora = DateTime.Now.TimeOfDay };
            MostrarPestanasRevision();
        }
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

        int ultima = revisiones.Max(r => r.Numero);
        btnNuevaRevision.Enabled = porRevision.GetValueOrDefault(ultima)?.Count > 0;

        int escritas = LeerObservaciones().Count(o => !string.IsNullOrWhiteSpace(o.Texto))
            + (string.IsNullOrWhiteSpace(txtDepositos.Text) ? 0 : 1);
        pestanaObservaciones.Text = "Observaciones" + (escritas > 0 ? $"  ·  {escritas}" : "");
    }

    private bool DescartarCambios() =>
        !hayCambios || Mensajes.DescartarCambios(this, "Hay cambios sin guardar en la ronda. ¿Descartarlos?");

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
