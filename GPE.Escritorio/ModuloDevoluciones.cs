using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Módulo de devoluciones: la lista con sus filtros. Vive dentro de FormPrincipal.
public class ModuloDevoluciones : UserControl
{

    private const int ColumnaEstado = 7;

    private readonly DevolucionServicio servicio = new();

    private readonly TextBox txtBuscar = new();
    private readonly ComboBox cmbEstado = new();
    private readonly DateTimePicker dtpDesde = new();
    private readonly DateTimePicker dtpHasta = new();
    private readonly Button btnLimpiar = Tema.BotonSecundario("Limpiar filtros", 140);
    private readonly Label lblContador = new();
    private readonly DataGridView grid = new();
    private readonly Label lblVacio = new();
    private readonly Button btnNueva = Tema.BotonPrincipal("+  Nueva devolución", 210);
    private readonly Button btnHistorial = Tema.BotonSecundario("Ver historial", 140);

    // Aviso de abonos pendientes: franja fija en la lista y ventana que se repite
    private const int MinutosEntreAvisos = 60;
    private readonly Panel franjaAviso = new();
    private readonly Label lblAviso = new();
    private readonly Button btnVerRetrasadas = Tema.BotonSecundario("Ver solo esas", 150);
    private readonly System.Windows.Forms.Timer temporizadorAviso = new() { Interval = MinutosEntreAvisos * 60_000 };
    private bool soloRetrasadas;    // filtro activado desde la franja

    // Tocaba avisar mientras había otra ventana abierta: FormPrincipal vuelve a llamar
    // a MostrarAvisoAbonos al volver a la ventana principal
    public bool AvisoPendiente { get; private set; }

    // Al elegir una devolución en el aviso: FormPrincipal cambia a este módulo antes de abrirla
    public event Action? PideMostrarse;

    public ModuloDevoluciones()
    {
        Dock = DockStyle.Fill;

        Controls.Add(CrearContenido());

        // FormPrincipal crea este módulo al arrancar (aunque se vea el inicio) para que
        // el aviso funcione desde el principio; la lista se carga la primera vez que se ve
        Load += (s, e) => CargarDatos();
        temporizadorAviso.Tick += (s, e) => MostrarAvisoAbonos();
        temporizadorAviso.Start();
        Disposed += (s, e) => temporizadorAviso.Dispose();
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
            RowCount = 5
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));   // franja de aviso: 0 si no se ve
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 90));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 56));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CrearCabecera(), 0, 0);
        layout.Controls.Add(CrearFranjaAviso(), 0, 1);
        layout.Controls.Add(CrearFiltros(), 0, 2);
        layout.Controls.Add(CrearBarraAcciones(), 0, 3);
        layout.Controls.Add(CrearZonaTabla(), 0, 4);

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
            Text = "Consulta y gestiona todas las devoluciones de clientes.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Suave
        };
        var lblTitulo = new Label
        {
            Text = "Devoluciones",
            Dock = DockStyle.Top,
            Height = 50,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = Tema.Texto
        };
        titulos.Controls.Add(lblSubtitulo);
        titulos.Controls.Add(lblTitulo);

        btnNueva.Height = 46;
        btnNueva.Visible = Sesion.TieneRol(Rol.Administracion);   // dar de alta es cosa de administración
        btnNueva.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnNueva.Margin = new Padding(0, 10, 0, 0);
        btnNueva.Click += (s, e) =>
        {
            using var formulario = new FormNuevaDevolucion();
            if (formulario.ShowDialog(this) == DialogResult.OK)
            {
                CargarDatos();
                SeleccionarDevolucion(formulario.IdCreado);
            }
        };

        cabecera.Controls.Add(titulos, 0, 0);
        cabecera.Controls.Add(btnNueva, 1, 0);
        return cabecera;
    }

    private Control CrearFranjaAviso()
    {
        franjaAviso.Dock = DockStyle.Fill;
        franjaAviso.Height = 50;
        franjaAviso.BackColor = Tema.AvisoFondo;
        franjaAviso.Padding = new Padding(15, 0, 10, 0);
        franjaAviso.Margin = new Padding(0, 0, 0, 10);
        franjaAviso.Visible = false;

        lblAviso.Dock = DockStyle.Fill;
        lblAviso.TextAlign = ContentAlignment.MiddleLeft;
        lblAviso.ForeColor = Tema.Aviso;
        lblAviso.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        lblAviso.AutoEllipsis = true;

        btnVerRetrasadas.Dock = DockStyle.Right;
        btnVerRetrasadas.Click += (s, e) =>
        {
            soloRetrasadas = !soloRetrasadas;
            AplicarFiltros();
        };

        var marcoBoton = new Panel { Dock = DockStyle.Right, Width = btnVerRetrasadas.Width, Padding = new Padding(0, 8, 0, 8) };
        marcoBoton.Controls.Add(btnVerRetrasadas);
        btnVerRetrasadas.Dock = DockStyle.Fill;

        franjaAviso.Controls.Add(lblAviso);
        franjaAviso.Controls.Add(marcoBoton);
        return franjaAviso;
    }

    private void ActualizarFranjaAviso(List<DevolucionFila> devoluciones)
    {
        var retrasadas = RetrasadasDelUsuario(devoluciones);
        franjaAviso.Visible = retrasadas.Count > 0;
        if (retrasadas.Count == 0)
        {
            soloRetrasadas = false;
            return;
        }

        int maximo = retrasadas.Max(d => DevolucionServicio.DiasSinAbono(d) ?? 0);
        lblAviso.Text = retrasadas.Count == 1
            ? $"⚠  {retrasadas[0].Numero} está sin abono (llegó {(maximo == 0 ? "hoy" : "hace " + DevolucionServicio.TextoDias(maximo))}): " +
              "el cliente está esperando."
            : $"⚠  {retrasadas.Count} devoluciones están sin abono " +
              $"(la más antigua llegó {(maximo == 0 ? "hoy" : "hace " + DevolucionServicio.TextoDias(maximo))}): " +
              "los clientes están esperando.";
    }

    // El aviso de abonos retrasados es para quien registra el abono
    private static List<DevolucionFila> RetrasadasDelUsuario(List<DevolucionFila> devoluciones) =>
        Sesion.TieneRol(Rol.Administracion)
            ? devoluciones.Where(DevolucionServicio.AbonoRetrasado).ToList()
            : new List<DevolucionFila>();

    // Ventana de aviso, al entrar y cada MinutosEntreAvisos mientras quede alguna pendiente.
    // Salta aunque se esté en otro módulo (FormPrincipal la llama al arrancar).
    public void MostrarAvisoAbonos()
    {
        if (Application.OpenForms.Count > 1)
        {
            AvisoPendiente = true;
            return;
        }
        AvisoPendiente = false;

        List<DevolucionFila> devoluciones;
        try
        {
            devoluciones = servicio.ObtenerDevolucionesVisibles();
        }
        catch (DbException)
        {
            return;   
        }
        ActualizarFranjaAviso(devoluciones);   // los días cuentan aunque nadie recargue la lista

        var retrasadas = RetrasadasDelUsuario(devoluciones);
        if (retrasadas.Count == 0) return;

        int? elegida;
        using (var aviso = new FormAvisoAbonos(retrasadas))
        {
            aviso.ShowDialog(FindForm());   // la ventana principal: puede que este módulo no se esté viendo
            elegida = aviso.DevolucionElegida;
        }
        if (elegida == null) return;

        PideMostrarse?.Invoke();
        CargarDatos();   // por si ha cambiado algo desde la última carga
        SeleccionarDevolucion(elegida.Value);
        if (DevolucionSeleccionada() is DevolucionFila devolucion && devolucion.Id == elegida)
            AbrirDevolucion(grid.SelectedRows[0].Index);
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
        txtBuscar.PlaceholderText = "Nº, cliente o albarán";

        cmbEstado.Width = 210;
        cmbEstado.DropDownStyle = ComboBoxStyle.DropDownList;

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
        panel.Controls.Add(Componentes.CampoFiltro("Estado", cmbEstado));
        panel.Controls.Add(Componentes.CampoFiltro("Fecha desde", dtpDesde));
        panel.Controls.Add(Componentes.CampoFiltro("Fecha hasta", dtpHasta));
        panel.Controls.Add(btnLimpiar);

        // Los filtros se aplican al momento, sin botón de "Filtrar"
        txtBuscar.TextChanged += (s, e) => AplicarFiltros();
        cmbEstado.SelectedIndexChanged += (s, e) => AplicarFiltros();
        dtpDesde.ValueChanged += (s, e) => AplicarFiltros();
        dtpHasta.ValueChanged += (s, e) => AplicarFiltros();
        btnLimpiar.Click += (s, e) => LimpiarFiltros();

        return panel;
    }

    // Contador de resultados y acciones sobre la devolución seleccionada
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

        var botones = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(0)
        };
        btnHistorial.Height = 34;
        btnHistorial.Margin = new Padding(0);
        botones.Controls.Add(btnHistorial);

        btnHistorial.Click += (s, e) => AbrirHistorial();

        barra.Controls.Add(lblContador, 0, 0);
        barra.Controls.Add(botones, 1, 0);
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

        grid.CellDoubleClick += (s, e) => AbrirDevolucion(e.RowIndex);
        grid.SelectionChanged += (s, e) => ActualizarBotones();
        grid.CurrentCellChanged += (s, e) => ActualizarBotones();
        grid.CellPainting += PintarEstado;

        // Clic derecho sobre una fila: la selecciona y muestra el menú
        var menu = new ContextMenuStrip();
        menu.Items.Add("Ver historial de cambios", null, (s, e) => AbrirHistorial());
        grid.ContextMenuStrip = menu;
        grid.CellMouseDown += (s, e) =>
        {
            if (e.Button != MouseButtons.Right || e.RowIndex < 0) return;
            grid.CurrentCell = grid.Rows[e.RowIndex].Cells[Math.Max(e.ColumnIndex, 0)];
        };
        menu.Opening += (s, e) => e.Cancel = DevolucionSeleccionada() == null;

        // Algo más espaciada que las tablas de las pantallas de paso, y la fila elegida en morado
        grid.ColumnHeadersHeight = 45;
        grid.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;
        grid.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);

        string[] columnas =
        {
            "Nº DEVOLUCIÓN", "CLIENTE", "CÓD. CLIENTE", "MOTIVO",
            "FECHA SOLICITUD", "RECOGIDA PREVISTA", "FECHA LLEGADA", "ESTADO"
        };
        foreach (string nombre in columnas)
            grid.Columns.Add(nombre, nombre);

        grid.Columns[0].DefaultCellStyle.ForeColor = Tema.Acento;
        grid.Columns[0].DefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        // El número entero, sin cortar: la columna mide lo que ocupa el número. Con un ancho
        // mínimo y "Fill", al maximizar la ventana esta columna se quedaba con todo el sobrante.
        grid.Columns[0].AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
        grid.Columns[0].MinimumWidth = 135;
        grid.Columns[ColumnaEstado].FillWeight = 140;
        grid.Columns[6].FillWeight = 150;   // fecha de llegada: puede llevar "⚠ N días"

        return grid;
    }

    // Dibuja el estado como una etiqueta redondeada con el color de su fase
    private void PintarEstado(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex != ColumnaEstado || e.Graphics == null) return;
        if (grid.Rows[e.RowIndex].Tag is not DevolucionFila devolucion) return;

        var (fondo, texto) = Tema.ColorFase(devolucion.Fase);
        Componentes.PintarEtiqueta(e, devolucion.Estado, fondo, texto);
    }

    // Datos y filtros

    private void CargarDatos()
    {
        List<DevolucionFila> devoluciones;
        try
        {
            devoluciones = servicio.ObtenerDevolucionesVisibles();

            if (cmbEstado.Items.Count == 0)
            {
                cmbEstado.Items.Add("Todos los estados");
                foreach (string estado in servicio.ObtenerEstadosVisibles())
                    cmbEstado.Items.Add(estado);
                cmbEstado.SelectedIndex = 0;
            }
        }
        catch (DbException ex)
        {
            MessageBox.Show(this,
                "No se pudieron cargar las devoluciones de la base de datos.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        grid.Rows.Clear();
        foreach (var devolucion in devoluciones)
            AnadirDevolucion(devolucion);

        ActualizarFranjaAviso(devoluciones);
        AplicarFiltros();
    }

    private void AnadirDevolucion(DevolucionFila d)
    {
        int i = grid.Rows.Add(
            d.Numero,
            d.Cliente,
            d.CodigoCliente,
            d.Motivo,
            Componentes.Fecha(d.FechaSolicitud),
            Componentes.Fecha(d.FechaRecogida),
            Componentes.Fecha(d.FechaLlegada),
            d.Estado);

        grid.Rows[i].Tag = d;

        // Está esperando el abono: se marca la fecha de llegada
        if (DevolucionServicio.AbonoRetrasado(d) && Sesion.TieneRol(Rol.Administracion))
        {
            var llegada = grid.Rows[i].Cells[6];
            llegada.Value = $"{Componentes.Fecha(d.FechaLlegada)} · {DevolucionServicio.TextoDias(DevolucionServicio.DiasSinAbono(d) ?? 0)}";
            llegada.Style.ForeColor = Tema.Aviso;
            llegada.Style.BackColor = Tema.AvisoFondo;
            llegada.Style.SelectionForeColor = Tema.Aviso;
            llegada.ToolTipText = "El cliente está esperando el abono";
        }
    }

    private void SeleccionarDevolucion(int id)
    {
        var fila = grid.Rows.Cast<DataGridViewRow>()
            .FirstOrDefault(f => f.Tag is DevolucionFila d && d.Id == id);

        if (fila == null) return;

        if (!fila.Visible && fila.Tag is DevolucionFila { Fase: < Fase.Cierre })
            LimpiarFiltros();
        if (!fila.Visible) return;   // se acaba de cerrar y no se está buscando: ya no está pendiente

        grid.ClearSelection();
        grid.CurrentCell = fila.Cells[0];
        fila.Selected = true;
        grid.FirstDisplayedScrollingRowIndex = fila.Index;
        ActualizarBotones();
    }

    private void AbrirDevolucion(int indiceFila)
    {
        if (indiceFila < 0) return;   // doble clic en la cabecera de la tabla
        if (grid.Rows[indiceFila].Tag is not DevolucionFila devolucion) return;

        // Se abre el paso en curso (el siguiente al completado); si está cerrada, el cierre
        int paso = Fase.PasoEnCurso(devolucion.Fase);

        // Al pulsar otro paso en la barra, la pantalla se cierra y se abre la de ese paso
        while (true)
        {
            int? siguiente;
            using (var pantalla = CrearPantalla(devolucion, paso))
            {
                pantalla.ShowDialog(this);
                siguiente = pantalla.PasoSolicitado;
            }
            if (siguiente == null) break;

            try
            {
                if (servicio.ObtenerDevolucion(devolucion.Id) is not DevolucionFila actualizada) break;
                devolucion = actualizada;   // puede haber cambiado de fase en la pantalla anterior
            }
            catch (DbException ex)
            {
                MessageBox.Show(this, "No se pudo cargar la devolución.\n\n" + ex.Message,
                    "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                break;
            }

            paso = Math.Min(siguiente.Value, Fase.PasoEnCurso(devolucion.Fase));   // nunca más allá del paso en curso
        }

        CargarDatos();
        SeleccionarDevolucion(devolucion.Id);
    }

    // Pantalla de cada paso de la barra de progreso (abono y cierre comparten pantalla)
    private static PantallaFase CrearPantalla(DevolucionFila devolucion, int paso) => paso switch
    {
        FormSolicitud.PasoSolicitud => new FormSolicitud(devolucion),
        Recogida.PasoRecogida => new Recogida(devolucion),
        FormAlmacen.PasoLlegada => new FormAlmacen(devolucion, FormAlmacen.PasoLlegada),
        FormAlmacen.PasoRevision => new FormAlmacen(devolucion, FormAlmacen.PasoRevision),
        _ => new FormAbono(devolucion, paso)
    };

    private void AbrirHistorial()
    {
        if (DevolucionSeleccionada() is not DevolucionFila devolucion) return;

        using var pantalla = new FormHistorial(devolucion);
        pantalla.ShowDialog(this);
    }

    private void AplicarFiltros()
    {
        string texto = txtBuscar.Text.Trim();
        string estado = cmbEstado.SelectedIndex > 0 ? cmbEstado.Text : "";
        DateTime? desde = dtpDesde.Checked ? dtpDesde.Value.Date : null;
        DateTime? hasta = dtpHasta.Checked ? dtpHasta.Value.Date : null;

        grid.CurrentCell = null;

        foreach (DataGridViewRow fila in grid.Rows)
        {
            string numero = fila.Cells[0].Value?.ToString() ?? "";
            string cli = fila.Cells[1].Value?.ToString() ?? "";
            string codigoCliente = fila.Cells[2].Value?.ToString() ?? "";
            string est = fila.Cells[ColumnaEstado].Value?.ToString() ?? "";

            bool coincideTexto = texto == ""
                || numero.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || cli.Contains(texto, StringComparison.OrdinalIgnoreCase)
                || codigoCliente.Contains(texto, StringComparison.OrdinalIgnoreCase);
            // Las cerradas solo salen al buscar una o al filtrar por su estado: así la lista
            // enseña lo pendiente y se puede consultar cualquier cerrada si hace falta
            bool cerrada = fila.Tag is DevolucionFila { Fase: Fase.Cierre };
            bool coincideEstado = estado == "" ? !cerrada || texto != "" : est == estado;

            bool coincideFecha = fila.Tag is not DevolucionFila d
                || ((desde == null || d.FechaSolicitud >= desde)
                    && (hasta == null || d.FechaSolicitud <= hasta));

            bool coincideRetraso = !soloRetrasadas
                || (fila.Tag is DevolucionFila r && DevolucionServicio.AbonoRetrasado(r));

            fila.Visible = coincideTexto && coincideEstado && coincideFecha && coincideRetraso;
        }

        btnVerRetrasadas.Text = soloRetrasadas ? "Ver todas" : "Ver solo esas";

        ActualizarContador(desde != null && hasta != null && desde > hasta);
        ActualizarBotones();
    }

    private void LimpiarFiltros()
    {
        txtBuscar.Clear();
        cmbEstado.SelectedIndex = 0;
        dtpDesde.Checked = false;
        dtpHasta.Checked = false;
        soloRetrasadas = false;
        AplicarFiltros();
    }

    private void ActualizarContador(bool fechasCruzadas)
    {
        int visibles = grid.Rows.Cast<DataGridViewRow>().Count(f => f.Visible);
        lblContador.Text = $"{visibles} {(visibles == 1 ? "devolución" : "devoluciones")}";

        lblVacio.Text = fechasCruzadas
            ? "La fecha \"desde\" es posterior a la fecha \"hasta\"."
            : !grid.Rows.Cast<DataGridViewRow>().Any(f => f.Tag is DevolucionFila { Fase: < Fase.Cierre })
              && cmbEstado.SelectedIndex <= 0 && txtBuscar.Text.Trim() == ""
                ? (Sesion.TieneRol(Rol.Administracion)
                    ? "No hay devoluciones pendientes. Crea una nueva con \"+ Nueva devolución\"."
                    : "No hay devoluciones pendientes para tu rol.")
                : "No hay devoluciones que coincidan con los filtros.";
        lblVacio.ForeColor = fechasCruzadas ? Tema.Error : Tema.Suave;
        lblVacio.Visible = visibles == 0;
        grid.Visible = visibles > 0;
    }

    // Se mira la fila seleccionada y no CurrentRow: al seleccionar por código (al volver de
    // una devolución), SelectionChanged salta antes de que CurrentRow se actualice.
    private DevolucionFila? DevolucionSeleccionada() =>
        grid.SelectedRows.Cast<DataGridViewRow>()
            .FirstOrDefault(f => f.Visible && f.Tag is DevolucionFila)?.Tag as DevolucionFila;

    private void ActualizarBotones()
    {
        btnHistorial.Enabled = DevolucionSeleccionada() != null;
    }
}
