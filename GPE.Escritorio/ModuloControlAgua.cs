using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Control de agua (la hoja AGUA, MT-RG09): como en la rutina de calidad, se elige el día y
// debajo se apuntan en una tabla los puntos de agua revisados, con el cloro y los
// organolépticos. Se usa también con una tablet, así que todo se hace pulsando: el punto se
// elige con botones grandes (ElegirOpcion), el cloro con un teclado de números
// (TecladoNumerico) y cada organoléptico cambia de Sí a No con un toque. Vive dentro de FormPrincipal.
public class ModuloControlAgua : UserControl
{
    private readonly AguaServicio servicio = new();
    private List<AguaToma> tomas = new();
    private DateTime fecha = DateTime.Today;
    private List<ControlAgua> originales = new();   // los del día tal como se leyeron
    private bool hayCambios;
    private bool cargando;

    private readonly DateTimePicker dtpFecha = new();
    private readonly Label lblDia = new();
    private readonly Label lblEstado = new();
    private readonly DataGridView grid = new();
    private readonly Button btnGuardar = Tema.BotonPrincipal("Guardar", 150);
    private readonly Button btnAnadir = Tema.BotonSecundario("+  Añadir punto de agua", 220);

    private const string ColToma = "toma", ColCloro = "cloro", ColSabor = "sabor", ColOlor = "olor",
        ColColor = "color", ColTurbidez = "turbidez", ColComentario = "comentario", ColQuitar = "quitar";
    private static readonly string[] ColumnasSiNo = { ColSabor, ColOlor, ColColor, ColTurbidez };

    public ModuloControlAgua()
    {
        Dock = DockStyle.Fill;
        Controls.Add(CrearContenido());
        Load += (s, e) =>
        {
            Cargar(DateTime.Today, recargarTomas: true);
            // La ventana principal no sabe de este día: avisa él al cerrar con cambios
            if (FindForm() is Form ventana) ventana.FormClosing += AvisarAlCerrar;
        };
    }

    private Control CrearContenido()
    {
        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(40, 30, 40, 20)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 95));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(CrearCabecera(), 0, 0);
        layout.Controls.Add(CrearBarra(), 0, 1);
        layout.Controls.Add(new Label
        {
            Text = "Pulsa \"+ Añadir punto de agua\" y elige el punto. Pulsa una casilla para cambiarla: " +
                   "el cloro abre un teclado y Sí / No cambia con cada toque. ✕ quita la fila.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Tema.Suave,
            Font = Tema.FuentePequena
        }, 0, 2);
        layout.Controls.Add(CrearZonaTabla(), 0, 3);
        return layout;
    }

    // La tabla mide lo que sus filas y "Añadir fila" va justo debajo de la última, como una
    // fila más; si no caben, se desplaza todo junto
    private Control CrearZonaTabla()
    {
        var zona = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        btnAnadir.Dock = DockStyle.Top;
        btnAnadir.Height = 52;
        btnAnadir.TextAlign = ContentAlignment.MiddleLeft;
        btnAnadir.Padding = new Padding(10, 0, 0, 0);
        btnAnadir.ForeColor = Tema.Acento;
        btnAnadir.FlatAppearance.BorderColor = Tema.LineaTabla;
        btnAnadir.FlatAppearance.MouseOverBackColor = Tema.AcentoSuave;
        btnAnadir.Click += (s, e) => AnadirFila();

        // El último añadido queda arriba
        zona.Controls.Add(btnAnadir);
        zona.Controls.Add(ConfigurarGrid());
        return zona;
    }

    private Control CrearCabecera()
    {
        var cabecera = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titulos = new Panel { Dock = DockStyle.Fill };
        titulos.Controls.Add(new Label
        {
            Text = "Cloro y organolépticos de los puntos de agua (MT-RG09).",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = Tema.Suave
        });
        titulos.Controls.Add(new Label
        {
            Text = "Control de agua",
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

    // El día, como en la rutina: calendario, flechas y "Hoy"
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

        // Botones algo más grandes que los de la rutina: aquí se cambia de día con el dedo
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
        lblDia.AutoSize = true;
        lblDia.ForeColor = Tema.Texto;
        lblDia.Font = Tema.FuenteNegrita;
        lblEstado.AutoSize = true;
        lblEstado.ForeColor = Tema.Suave;
        lblEstado.Margin = new Padding(0, 4, 0, 0);
        datos.Controls.Add(lblDia);
        datos.Controls.Add(lblEstado);

        var btnRecargar = Tema.BotonSecundario("Recargar", 110);
        btnRecargar.Height = 40;
        btnRecargar.Margin = new Padding(0, 12, 0, 0);
        btnRecargar.Click += (s, e) =>
        {
            if (DescartarCambios()) Cargar(fecha, recargarTomas: true);
        };

        barra.Controls.Add(filtros, 0, 0);
        barra.Controls.Add(datos, 1, 0);
        barra.Controls.Add(btnRecargar, 2, 0);
        return barra;
    }

    private Control ConfigurarGrid()
    {
        Componentes.EstiloTabla(grid, altoFila: 48);
        grid.Dock = DockStyle.Top;
        Componentes.AjustarAltoTabla(grid);
        grid.RowsAdded += (s, e) => Componentes.AjustarAltoTabla(grid);
        grid.RowsRemoved += (s, e) => Componentes.AjustarAltoTabla(grid);
        grid.AllowUserToDeleteRows = true;   // Supr, además de ✕
        // Solo el comentario se escribe en la celda; lo demás se cambia al pulsarlo (CeldaPulsada)
        grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        grid.GridColor = Tema.LineaTabla;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.MultiSelect = false;

        var toma = Columna(ColToma, "PUNTO DE AGUA", 0);
        toma.AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
        toma.FillWeight = 160;
        grid.Columns.Add(toma);

        var cloro = Columna(ColCloro, "CLORO (ppm)", 120);
        cloro.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        cloro.DefaultCellStyle.Font = Tema.FuenteNegrita;
        grid.Columns.Add(cloro);

        foreach (var (nombre, cabecera) in new[] { (ColSabor, "SABOR OK"), (ColOlor, "OLOR OK"), (ColColor, "COLOR OK"), (ColTurbidez, "TURBIDEZ OK") })
            grid.Columns.Add(Columna(nombre, cabecera, 110));

        grid.Columns.Add(new DataGridViewTextBoxColumn
        {
            Name = ColComentario,
            HeaderText = "COMENTARIO",
            FillWeight = 140,
            MaxInputLength = AguaServicio.LargoComentario,
            SortMode = DataGridViewColumnSortMode.NotSortable
        });

        var quitar = Columna(ColQuitar, "", 56);
        quitar.ToolTipText = "Quitar la fila";
        grid.Columns.Add(quitar);

        grid.CellClick += CeldaPulsada;
        grid.KeyDown += TeclaPulsada;
        grid.CellPainting += PintarCelda;
        grid.CellValueChanged += (s, e) => { if (!cargando) MarcarCambios(); };
        grid.UserDeletedRow += (s, e) => MarcarCambios();
        grid.CellFormatting += Formatear;
        return grid;
    }

    // Columna que no se escribe: se cambia al pulsarla. ancho 0: la de ancho variable.
    private static DataGridViewTextBoxColumn Columna(string nombre, string cabecera, int ancho)
    {
        var columna = new DataGridViewTextBoxColumn
        {
            Name = nombre,
            HeaderText = cabecera,
            ReadOnly = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (ancho > 0)
        {
            columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            columna.Width = ancho;
        }
        return columna;
    }

    // El nombre del punto de agua (la celda guarda su id) y el cloro fuera del rango del
    // punto (si lo tiene), en rojo
    private void Formatear(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle == null) return;
        var fila = grid.Rows[e.RowIndex];
        string columna = grid.Columns[e.ColumnIndex].Name;

        if (columna == ColToma)
        {
            // Un punto que ya no está en la lista (dado de baja) no rompe la tabla
            e.Value = Toma(fila)?.Nombre ?? "—";
            e.FormattingApplied = true;
        }
        else if (columna == ColCloro && Toma(fila) is AguaToma toma && LeerCloro(fila, out var cloro)
            && AguaServicio.FueraDeRango(cloro, toma))
        {
            e.CellStyle.BackColor = e.CellStyle.SelectionBackColor = Tema.FondoError;
            e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Tema.Error;
        }
    }

    // Sí / No de los organolépticos como una etiqueta verde o roja, y la ✕ de quitar la fila
    private void PintarCelda(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Graphics == null) return;
        string columna = grid.Columns[e.ColumnIndex].Name;

        if (ColumnasSiNo.Contains(columna))
        {
            bool si = e.Value is true;
            var (fondo, texto) = Tema.ColorEstadoCasilla(si ? EstadoCasilla.Correcto : EstadoCasilla.NoCorrecto);
            Componentes.PintarEtiqueta(e, si ? "✓  Sí" : "✗  No", fondo, texto);
        }
        else if (columna == ColQuitar)
        {
            e.PaintBackground(e.CellBounds, false);
            TextRenderer.DrawText(e.Graphics, "✕", Tema.FuenteNegrita, e.CellBounds, Tema.Suave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            e.Handled = true;
        }
    }

    // Filas

    // Al pulsar una celda: el punto y el cloro abren su panel, Sí / No cambia, el comentario
    // se empieza a escribir y ✕ quita la fila
    private void CeldaPulsada(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
        var fila = grid.Rows[e.RowIndex];
        string columna = grid.Columns[e.ColumnIndex].Name;

        if (columna == ColToma) ElegirPunto(fila);
        else if (columna == ColCloro) PedirCloro(fila);
        else if (ColumnasSiNo.Contains(columna)) CambiarSiNo(fila.Cells[columna]);
        else if (columna == ColComentario) grid.BeginEdit(true);
        else if (columna == ColQuitar) QuitarFila(fila);
    }

    // Con teclado: espacio cambia Sí / No; Enter o F2 abren el panel del punto o del cloro
    private void TeclaPulsada(object? sender, KeyEventArgs e)
    {
        if (grid.CurrentCell is not DataGridViewCell celda || celda.RowIndex < 0 || grid.IsCurrentCellInEditMode) return;
        var fila = grid.Rows[celda.RowIndex];
        string columna = grid.Columns[celda.ColumnIndex].Name;

        if (e.KeyCode == Keys.Space && ColumnasSiNo.Contains(columna)) CambiarSiNo(celda);
        else if (e.KeyCode is Keys.Enter or Keys.F2 && columna == ColToma) ElegirPunto(fila);
        else if (e.KeyCode is Keys.Enter or Keys.F2 && columna == ColCloro) PedirCloro(fila);
        else return;
        e.Handled = e.SuppressKeyPress = true;
    }

    private static void CambiarSiNo(DataGridViewCell celda) => celda.Value = celda.Value is not true;

    // Esquina de abajo a la izquierda de una celda, en la pantalla: ahí se abren los paneles
    private Point DebajoDe(DataGridViewRow fila, string columna)
    {
        var celda = grid.GetCellDisplayRectangle(grid.Columns[columna]!.Index, fila.Index, false);
        return grid.PointToScreen(new Point(celda.Left, celda.Bottom));
    }

    // Los puntos de agua para elegir el de una fila (null: una nueva). Un punto puede
    // apuntarse más de una vez el mismo día.
    private ElegirOpcion PanelPuntos(DataGridViewRow? fila) =>
        new("Punto de agua",
            tomas.Select(t => (t.Id.ToString(), t.Nombre, true)).ToList(),
            (fila?.Cells[ColToma].Value as int?)?.ToString());

    // Se elige el punto y se añade la fila, con todo "Sí"; luego se abre el teclado del cloro,
    // que es lo siguiente que se apunta
    private void AnadirFila()
    {
        grid.EndEdit();
        var panel = PanelPuntos(null);
        panel.Abrir(this, btnAnadir.PointToScreen(new Point(0, btnAnadir.Height)), () =>
        {
            if (panel.Elegida is not string clave) return;
            int i = grid.Rows.Add(int.Parse(clave), "", true, true, true, true, "", "");
            // Con muchas filas, que se siga viendo la nueva y el botón
            if (btnAnadir.Parent is ScrollableControl zona) zona.ScrollControlIntoView(btnAnadir);
            grid.CurrentCell = grid.Rows[i].Cells[ColCloro];
            MarcarCambios();
            BeginInvoke(() => PedirCloro(grid.Rows[i]));
        });
    }

    private void ElegirPunto(DataGridViewRow fila)
    {
        var panel = PanelPuntos(fila);
        panel.Abrir(this, DebajoDe(fila, ColToma), () =>
        {
            if (fila.DataGridView != null && panel.Elegida is string clave && !Equals(fila.Cells[ColToma].Value, int.Parse(clave)))
                fila.Cells[ColToma].Value = int.Parse(clave);
        });
    }

    private void PedirCloro(DataGridViewRow fila)
    {
        string actual = fila.Cells[ColCloro].Value as string ?? "";
        var panel = new TecladoNumerico($"Cloro (ppm) · {Toma(fila)?.Nombre}", actual, largoMaximo: 4);
        panel.Abrir(this, DebajoDe(fila, ColCloro), () =>
        {
            if (fila.DataGridView != null && panel.Valor is string valor && valor != actual)
                fila.Cells[ColCloro].Value = valor;
        });
    }

    private void QuitarFila(DataGridViewRow fila)
    {
        if (!Mensajes.Confirmar(this, "Quitar fila", $"¿Quitar {Toma(fila)?.Nombre ?? "esta fila"} del control de este día?"))
            return;
        grid.Rows.Remove(fila);
        MarcarCambios();
    }

    private AguaToma? Toma(DataGridViewRow fila) =>
        fila.Cells[ColToma].Value is int id ? tomas.FirstOrDefault(t => t.Id == id) : null;

    // false si lo escrito no es un número. Acepta coma o punto.
    private static bool LeerCloro(DataGridViewRow fila, out decimal? cloro)
    {
        cloro = null;
        string texto = (fila.Cells[ColCloro].Value as string ?? "").Trim().Replace('.', ',');
        if (texto == "") return true;
        if (!decimal.TryParse(texto, System.Globalization.NumberStyles.Number, Componentes.Es, out var valor)) return false;
        cloro = valor;
        return true;
    }

    // Las filas como controles. Si un cloro no es un número, el mensaje para enseñar.
    private (List<ControlAgua> Controles, string? Error) LeerFilas()
    {
        var controles = new List<ControlAgua>();
        foreach (DataGridViewRow fila in grid.Rows)
        {
            if (fila.IsNewRow) continue;
            if (!LeerCloro(fila, out var cloro))
                return (controles, $"En la fila {fila.Index + 1}, el cloro tiene que ser un número (por ejemplo 0,55).");

            var original = fila.Tag as ControlAgua;
            controles.Add(new ControlAgua(
                Id: original?.Id ?? 0,
                Fecha: fecha,
                TomaId: fila.Cells[ColToma].Value as int? ?? 0,
                Cloro: cloro,
                SaborOk: fila.Cells[ColSabor].Value is true,
                OlorOk: fila.Cells[ColOlor].Value is true,
                ColorOk: fila.Cells[ColColor].Value is true,
                TurbidezOk: fila.Cells[ColTurbidez].Value is true,
                Comentario: fila.Cells[ColComentario].Value as string,
                CreadoPor: original?.CreadoPor,
                Version: original?.Version));
        }
        return (controles, null);
    }

    // Cargar y guardar

    private void Cargar(DateTime dia, bool recargarTomas)
    {
        try
        {
            if (recargarTomas) tomas = servicio.ObtenerTomas();
            originales = servicio.ObtenerDia(dia);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo cargar el control de agua de la base de datos.", ex);
            return;
        }
        fecha = dia.Date;

        cargando = true;
        dtpFecha.MaxDate = DateTime.Today;   // por si GPE lleva abierto desde ayer
        dtpFecha.Value = fecha;
        grid.Rows.Clear();
        foreach (var control in originales)
        {
            int i = grid.Rows.Add(control.TomaId, control.Cloro?.ToString("0.00", Componentes.Es) ?? "",
                control.SaborOk, control.OlorOk, control.ColorOk, control.TurbidezOk, control.Comentario ?? "", "");
            var fila = grid.Rows[i];
            fila.Tag = control;
            fila.Cells[ColToma].ToolTipText = $"Apuntado por {control.CreadoPor}";
        }
        grid.ClearSelection();
        Componentes.AjustarAltoTabla(grid);
        cargando = false;

        hayCambios = false;
        btnGuardar.Enabled = false;
        ActualizarResumen();
    }

    private void CambiarDia(DateTime dia)
    {
        if (dia == fecha) return;
        if (!DescartarCambios())
        {
            cargando = true;
            dtpFecha.Value = fecha;
            cargando = false;
            return;
        }
        Cargar(dia, recargarTomas: false);
    }

    private void Guardar()
    {
        grid.EndEdit();
        var (controles, error) = LeerFilas();
        try
        {
            if (error != null) throw new ReglaNegocioException(error);
            servicio.GuardarDia(fecha, controles, originales);
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Aviso(this, "No se puede guardar", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar el control de agua.", ex);
            return;
        }

        // Se vuelve a leer para tener los ids y las versiones nuevas
        Cargar(fecha, recargarTomas: false);
    }

    private void MarcarCambios()
    {
        hayCambios = true;
        btnGuardar.Enabled = true;
        ActualizarResumen();
    }

    private void ActualizarResumen()
    {
        string dia = fecha == DateTime.Today ? "hoy" : Componentes.Fecha(fecha);
        lblDia.Text = $"Control de agua de {dia}";

        int filas = grid.Rows.Cast<DataGridViewRow>().Count(f => !f.IsNewRow);
        lblEstado.Text = string.Join("  ·  ", new[]
        {
            filas == 0 ? "Sin controles" : $"{filas} {(filas == 1 ? "punto revisado" : "puntos revisados")}",
            hayCambios ? "Cambios sin guardar" : null
        }.OfType<string>());
        lblEstado.ForeColor = hayCambios ? Tema.Aviso : Tema.Suave;
    }

    private bool DescartarCambios() =>
        !hayCambios || Mensajes.DescartarCambios(this, "Hay cambios sin guardar en el control de agua. ¿Descartarlos?");

    private void AvisarAlCerrar(object? sender, FormClosingEventArgs e)
    {
        if (!e.Cancel && !DescartarCambios()) e.Cancel = true;
    }
}
