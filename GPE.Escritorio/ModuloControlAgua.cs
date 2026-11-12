using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Control de agua (hoja AGUA, MT-RG09): las tomas de un mes, la más reciente arriba.
// No se hacen todos los días: cada toma se apunta como una fila nueva con "+ Nueva toma" y se guarda con "Guardar"; las guardadas se corrigen en su ventana.
// Si una no convence se repite: la nueva queda como "repetición" y la otra como "repetida".
public class ModuloControlAgua : UserControl
{
    private readonly AguaServicio servicio = new();
    private List<AguaToma> tomas = new();
    private DateTime mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private List<ControlAguaConToma> controles = new();
    private HashSet<int> repetidas = new();   // ids de las tomas que alguna otra repite
    private readonly List<Pendiente> nuevas = new();   // filas nuevas, aún sin guardar

    private readonly Label lblMes = new();
    private readonly Button btnSiguiente;
    private readonly Button btnGuardar = Tema.BotonPrincipal("Guardar", 150);
    private readonly Button btnNueva = Tema.BotonSecundario("+  Nueva toma", 220);
    private readonly ComboBox cmbPunto = new();
    private readonly Label lblResumen = new();
    private readonly DataGridView grid = new();
    private readonly Label lblVacio = new();

    private const string ColFecha = "fecha", ColHora = "hora", ColToma = "toma", ColCloro = "cloro", ColSabor = "sabor",
        ColOlor = "olor", ColColor = "color", ColTurbidez = "turbidez", ColComentario = "comentario",
        ColRepeticion = "repeticion", ColRepetir = "repetir";
    private static readonly string[] ColumnasSiNo = { ColSabor, ColOlor, ColColor, ColTurbidez };

    // Opción del filtro de punto: null = todos
    private record OpcionPunto(int? Id, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    public ModuloControlAgua()
    {
        Dock = DockStyle.Fill;
        btnSiguiente = Flecha("▶", 1);
        Controls.Add(CrearContenido());
        Load += (s, e) =>
        {
            Cargar(recargarTomas: true);
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
            Text = "Pulsa \"+ Nueva toma\" (debajo de la tabla), elige el punto y apunta el cloro; Sí / No cambia con cada toque. " +
                   "Pulsa \"Guardar\" para guardarlas. Doble clic en una guardada para corregirla o borrarla. " +
                   "\"Repetir\" apunta otra del mismo punto si el resultado no convence.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = Tema.Suave,
            Font = Tema.FuentePequena
        }, 0, 2);
        layout.Controls.Add(CrearZonaTabla(), 0, 3);
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

    // El mes con flechas, el punto y el resumen; a la derecha la gráfica y recargar
    private Control CrearBarra()
    {
        var barra = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            BackColor = Color.White,
            Padding = new Padding(15, 8, 15, 0)
        };
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        barra.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        lblMes.AutoSize = false;
        lblMes.Size = new Size(170, 40);
        lblMes.TextAlign = ContentAlignment.MiddleCenter;
        lblMes.Font = Tema.FuenteNegrita;
        lblMes.Margin = new Padding(0, 18, 6, 0);

        var btnEsteMes = Tema.BotonSecundario("Este mes", 100);
        btnEsteMes.Height = 40;
        btnEsteMes.Margin = new Padding(0, 18, 30, 0);
        btnEsteMes.Click += (s, e) => CambiarMes(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));

        cmbPunto.Width = 300;
        cmbPunto.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbPunto.MaxDropDownItems = 15;
        cmbPunto.SelectedIndexChanged += (s, e) => Mostrar();

        lblResumen.AutoSize = true;
        lblResumen.ForeColor = Tema.Suave;
        lblResumen.Margin = new Padding(20, 28, 0, 0);

        var filtros = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        filtros.Controls.Add(Flecha("◀", -1));
        filtros.Controls.Add(lblMes);
        filtros.Controls.Add(btnSiguiente);
        filtros.Controls.Add(btnEsteMes);
        filtros.Controls.Add(Componentes.CampoFiltro("Punto de agua", cmbPunto));
        filtros.Controls.Add(lblResumen);

        var btnGrafica = Tema.BotonSecundario("Ver gráfica", 130);
        btnGrafica.Height = 40;
        btnGrafica.Margin = new Padding(0, 12, 10, 0);
        btnGrafica.Click += (s, e) =>
        {
            using var ventana = new FormAguaGrafica(mes, tomas);
            ventana.ShowDialog(this);
        };
        var btnRecargar = Tema.BotonSecundario("Recargar", 110);
        btnRecargar.Height = 40;
        btnRecargar.Margin = new Padding(0, 12, 0, 0);
        btnRecargar.Click += (s, e) =>
        {
            if (DescartarNuevas()) Cargar(recargarTomas: true);
        };

        var botones = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0) };
        botones.Controls.Add(btnGrafica);
        botones.Controls.Add(btnRecargar);

        barra.Controls.Add(filtros, 0, 0);
        barra.Controls.Add(botones, 1, 0);
        return barra;
    }

    private Button Flecha(string texto, int meses)
    {
        var boton = Tema.BotonSecundario(texto, 48);
        boton.Height = 40;
        boton.Margin = new Padding(0, 18, 6, 0);
        boton.Click += (s, e) => CambiarMes(mes.AddMonths(meses));
        return boton;
    }

    // Tabla

    private Control CrearZonaTabla()
    {
        // "+ Nueva toma" va justo debajo de la última fila; si no cabe, se desplaza todo junto
        var zona = new Panel { Dock = DockStyle.Fill, AutoScroll = true };

        btnNueva.Dock = DockStyle.Top;
        btnNueva.Height = 52;
        btnNueva.TextAlign = ContentAlignment.MiddleLeft;
        btnNueva.Padding = new Padding(10, 0, 0, 0);
        btnNueva.ForeColor = Tema.Acento;
        btnNueva.FlatAppearance.BorderColor = Tema.LineaTabla;
        btnNueva.FlatAppearance.MouseOverBackColor = Tema.AcentoSuave;
        btnNueva.Click += (s, e) => NuevaToma();

        lblVacio.Dock = DockStyle.Top;
        lblVacio.Height = 90;
        lblVacio.BackColor = Color.White;
        lblVacio.ForeColor = Tema.Suave;
        lblVacio.TextAlign = ContentAlignment.MiddleCenter;
        lblVacio.Visible = false;

        // Con Dock arriba, el último añadido queda más arriba
        zona.Controls.Add(btnNueva);
        zona.Controls.Add(lblVacio);
        zona.Controls.Add(ConfigurarGrid());
        return zona;
    }

    private Control ConfigurarGrid()
    {
        Componentes.EstiloTabla(grid, altoFila: 44);
        grid.Dock = DockStyle.Top;
        Componentes.AjustarAltoTabla(grid);
        grid.RowsAdded += (s, e) => Componentes.AjustarAltoTabla(grid);
        grid.RowsRemoved += (s, e) => Componentes.AjustarAltoTabla(grid);
        // Todo se cambia tocando la casilla; solo la hora y el comentario se escriben
        grid.EditMode = DataGridViewEditMode.EditOnKeystrokeOrF2;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ShowCellToolTips = true;
        grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;
        grid.DefaultCellStyle.SelectionForeColor = Tema.Texto;

        (string Nombre, string Cabecera, int Ancho)[] columnas =
        {
            (ColFecha, "FECHA", 115), (ColHora, "HORA", 75), (ColToma, "PUNTO DE AGUA", 0), (ColCloro, "CLORO (ppm)", 110),
            (ColSabor, "SABOR OK", 95), (ColOlor, "OLOR OK", 95), (ColColor, "COLOR OK", 95), (ColTurbidez, "TURBIDEZ OK", 105),
            (ColComentario, "COMENTARIO", 0), (ColRepeticion, "", 130), (ColRepetir, "", 125)
        };
        foreach (var (nombre, cabecera, ancho) in columnas)
        {
            var columna = new DataGridViewTextBoxColumn
            {
                Name = nombre,
                HeaderText = cabecera,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            if (ancho > 0)
            {
                columna.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                columna.Width = ancho;
            }
            grid.Columns.Add(columna);
        }
        grid.Columns[ColToma]!.FillWeight = 120;
        grid.Columns[ColComentario]!.FillWeight = 100;
        grid.Columns[ColHora]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
        grid.Columns[ColCloro]!.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        grid.Columns[ColCloro]!.DefaultCellStyle.Font = Tema.FuenteNegrita;
        grid.Columns[ColRepetir]!.ToolTipText = "Apuntar otra toma de este punto porque esta no convence";

        grid.CellPainting += PintarCelda;
        grid.CellFormatting += Formatear;
        // Se deja para después del clic, porque la tabla se puede volver a montar
        grid.CellClick += (s, e) =>
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;
            var fila = grid.Rows[e.RowIndex];
            string columna = grid.Columns[e.ColumnIndex].Name;
            if (fila.Tag is Pendiente) BeginInvoke(() => PulsarNueva(fila, columna));
            else if (fila.Tag is ControlAguaConToma toma && columna == ColRepetir) BeginInvoke(() => Repetir(toma));
        };
        grid.CellValueChanged += (s, e) =>
        {
            if (e.RowIndex >= 0) CopiarAFila(grid.Rows[e.RowIndex]);
        };
        // Las guardadas se corrigen o se borran en la ventana
        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && (e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != ColRepetir))
                Corregir(grid.Rows[e.RowIndex]);
        };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || grid.CurrentRow?.Tag is not ControlAguaConToma) return;
            e.Handled = true;   // si no, Enter baja a la fila siguiente
            Corregir(grid.CurrentRow);
        };
        return grid;
    }

    private bool EsRepetida(DataGridViewRow fila) =>
        fila.Tag is ControlAguaConToma c && repetidas.Contains(c.Control.Id);

    // Sí / No como etiquetas, "repetición" / "repetida" y el botón de repetir
    private void PintarCelda(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Graphics == null) return;
        var fila = grid.Rows[e.RowIndex];
        string columna = grid.Columns[e.ColumnIndex].Name;
        if (fila.Tag is Pendiente nueva)
        {
            PintarNueva(e, columna, nueva);
            return;
        }
        if (fila.Tag is not ControlAguaConToma toma) return;

        if (ColumnasSiNo.Contains(columna))
            PintarSiNo(e);
        else if (columna == ColRepeticion)
        {
            if (EsRepetida(fila))
                Componentes.PintarEtiqueta(e, "repetida", Tema.CabeceraTabla, Tema.Suave);
            else if (toma.Control.RepiteId != null)
                Componentes.PintarEtiqueta(e, "↻  repetición", Tema.AcentoSuave, Tema.Acento);
            else
            {
                e.PaintBackground(e.CellBounds, true);
                e.Handled = true;
            }
        }
        else if (columna == ColRepetir)
            PintarBoton(e, EsRepetida(fila) ? null : "↻  Repetir", Tema.Acento);
    }

    // Fila sin guardar: Sí / No, "sin guardar" o "repetición" y "✕ Quitar"
    private static void PintarNueva(DataGridViewCellPaintingEventArgs e, string columna, Pendiente p)
    {
        if (ColumnasSiNo.Contains(columna))
            PintarSiNo(e);
        else if (columna == ColRepeticion)
            Componentes.PintarEtiqueta(e, p.RepiteId != null ? "↻  repetición" : "sin guardar", Tema.AcentoSuave, Tema.Acento);
        else if (columna == ColRepetir)
            PintarBoton(e, "✕  Quitar", Tema.Error);
    }

    private static void PintarSiNo(DataGridViewCellPaintingEventArgs e)
    {
        bool si = e.Value is true;
        var (fondo, texto) = Tema.ColorEstadoCasilla(si ? EstadoCasilla.Correcto : EstadoCasilla.NoCorrecto);
        Componentes.PintarEtiqueta(e, si ? "✓  Sí" : "✗  No", fondo, texto);
    }

    // Un botón dibujado en la celda (sin texto, la celda vacía)
    private static void PintarBoton(DataGridViewCellPaintingEventArgs e, string? texto, Color color)
    {
        e.PaintBackground(e.CellBounds, true);
        if (texto != null)
        {
            var caja = Rectangle.Inflate(e.CellBounds, -10, -8);
            using (var borde = new Pen(Tema.LineaTabla))
                e.Graphics!.DrawRectangle(borde, caja);
            TextRenderer.DrawText(e.Graphics, texto, Tema.FuentePequenaNegrita, caja, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }
        e.Handled = true;
    }

    // Las repetidas en gris; el cloro fuera del límite del punto, en rojo
    private void Formatear(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.CellStyle == null) return;
        var fila = grid.Rows[e.RowIndex];
        if (fila.Tag is not ControlAguaConToma toma) return;

        if (EsRepetida(fila))
        {
            e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Tema.Suave;
            if (grid.Columns[e.ColumnIndex].Name == ColCloro) e.CellStyle.Font = Tema.Fuente;
        }
        if (grid.Columns[e.ColumnIndex].Name == ColCloro && AguaServicio.FueraDeRango(toma.Control.Cloro, toma.Toma))
        {
            e.CellStyle.BackColor = e.CellStyle.SelectionBackColor = Tema.FondoError;
            e.CellStyle.ForeColor = e.CellStyle.SelectionForeColor = Tema.Error;
        }
    }

    // Datos

    private void CambiarMes(DateTime nuevo)
    {
        if (nuevo > DateTime.Today || nuevo == mes) return;
        mes = nuevo;
        Cargar(recargarTomas: false);
    }

    private void Cargar(bool recargarTomas)
    {
        try
        {
            if (recargarTomas) tomas = servicio.ObtenerTomas();
            controles = servicio.ObtenerMes(mes);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo cargar el control de agua de la base de datos.", ex);
            controles = new();
        }
        repetidas = controles.Where(c => c.Control.RepiteId != null).Select(c => c.Control.RepiteId!.Value).ToHashSet();

        // "Noviembre 2026"
        string nombre = mes.ToString("MMMM yyyy", Componentes.Es);
        lblMes.Text = char.ToUpper(nombre[0], Componentes.Es) + nombre[1..];
        btnSiguiente.Enabled = mes.AddMonths(1) <= DateTime.Today;

        LlenarPuntos();
        Mostrar();
    }

    // Los puntos activos y los dados de baja que salgan en el mes; se mantiene el elegido al cambiar de mes
    private void LlenarPuntos()
    {
        int? elegido = (cmbPunto.SelectedItem as OpcionPunto)?.Id;

        var puntos = tomas.Select(t => new OpcionPunto(t.Id, t.Nombre)).ToList();
        foreach (var toma in controles.Select(c => c.Toma).DistinctBy(t => t.Id))
            if (!puntos.Any(p => p.Id == toma.Id))
                puntos.Add(new OpcionPunto(toma.Id, toma.Nombre));
        if (elegido != null && !puntos.Any(p => p.Id == elegido))
            elegido = null;

        cmbPunto.BeginUpdate();
        cmbPunto.Items.Clear();
        cmbPunto.Items.Add(new OpcionPunto(null, "Todos"));
        cmbPunto.Items.AddRange(puntos.ToArray());
        cmbPunto.SelectedItem = cmbPunto.Items.Cast<OpcionPunto>().First(p => p.Id == elegido);
        cmbPunto.EndUpdate();
    }

    // La más reciente arriba; las nuevas, sin guardar, abajo junto a "+ Nueva toma"
    private void Mostrar()
    {
        int? punto = (cmbPunto.SelectedItem as OpcionPunto)?.Id;
        var visibles = controles
            .Where(c => punto == null || c.Toma.Id == punto)
            .OrderByDescending(c => c.Control.Fecha).ThenByDescending(c => c.Control.Hora).ThenByDescending(c => c.Control.Id)
            .ToList();

        grid.EndEdit();
        grid.Rows.Clear();
        foreach (var fila in visibles)
        {
            var c = fila.Control;
            int i = grid.Rows.Add(
                c.Fecha.ToString("ddd dd/MM", Componentes.Es),
                c.Hora is TimeSpan h ? AguaServicio.Hora(h) : "",
                fila.Toma.Nombre,
                c.Cloro?.ToString("0.00", Componentes.Es) ?? "—",
                c.SaborOk, c.OlorOk, c.ColorOk, c.TurbidezOk,
                c.Comentario?.Split('\n')[0] ?? "",
                "", "");
            var row = grid.Rows[i];
            row.Tag = fila;
            row.ReadOnly = true;
            row.Cells[ColToma].ToolTipText = $"Apuntada por {c.CreadoPor}";
            if (c.Comentario != null) row.Cells[ColComentario].ToolTipText = c.Comentario;
        }
        // En cualquier mes y con cualquier punto
        foreach (var p in nuevas) AnadirNueva(p);
        grid.ClearSelection();

        int repetidasVisibles = visibles.Count(c => repetidas.Contains(c.Control.Id));
        int fuera = visibles.Count(c => !repetidas.Contains(c.Control.Id) && AguaServicio.FueraDeRango(c.Control.Cloro, c.Toma));
        lblResumen.Text = string.Join("  ·  ", new[]
        {
            visibles.Count == 0 ? null : $"{visibles.Count} {(visibles.Count == 1 ? "toma" : "tomas")}",
            repetidasVisibles == 0 ? null : $"{repetidasVisibles} {(repetidasVisibles == 1 ? "repetida" : "repetidas")}",
            fuera == 0 ? null : $"{fuera} fuera del límite",
            nuevas.Count == 0 ? null : $"{nuevas.Count} sin guardar"
        }.OfType<string>());
        lblResumen.ForeColor = nuevas.Count > 0 ? Tema.Aviso : fuera > 0 ? Tema.Error : Tema.Suave;
        btnGuardar.Enabled = nuevas.Count > 0;

        lblVacio.Text = punto == null ? "No hay tomas de agua apuntadas en este mes." : "Este punto no tiene tomas en este mes.";
        lblVacio.Visible = visibles.Count == 0 && nuevas.Count == 0;
        grid.Visible = visibles.Count > 0 || nuevas.Count > 0;
    }

    // Tomas nuevas: se apuntan como filas en la tabla y se guardan con "Guardar"

    // Una fila nueva sin guardar. Hora y cloro como se han escrito; se comprueban al guardar.
    private class Pendiente
    {
        public DateTime Fecha;
        public int TomaId;
        public string Hora = "";
        public string Cloro = "";
        public bool Sabor = true, Olor = true, Color = true, Turbidez = true;
        public string Comentario = "";
        public int? RepiteId;   // la toma que repite (no se puede cambiar de punto)
    }

    private void AnadirNueva(Pendiente p)
    {
        int i = grid.Rows.Add(
            p.Fecha == DateTime.Today ? "hoy" : p.Fecha.ToString("ddd dd/MM", Componentes.Es),
            p.Hora,
            NombrePunto(p.TomaId),
            p.Cloro,
            p.Sabor, p.Olor, p.Color, p.Turbidez,
            p.Comentario,
            "", "");
        var row = grid.Rows[i];
        row.Tag = p;
        row.DefaultCellStyle.BackColor = Tema.AvisoFondo;
        // Solo la hora y el comentario se escriben; lo demás se cambia al pulsarlo
        foreach (DataGridViewCell celda in row.Cells)
            celda.ReadOnly = grid.Columns[celda.ColumnIndex].Name is not (ColHora or ColComentario);
    }

    private string NombrePunto(int id) =>
        tomas.FirstOrDefault(t => t.Id == id)?.Nombre
        ?? controles.FirstOrDefault(c => c.Toma.Id == id)?.Toma.Nombre ?? "—";

    // Lo escrito en la tabla pasa a su fila
    private void CopiarAFila(DataGridViewRow row)
    {
        if (row.Tag is not Pendiente p) return;
        p.Hora = row.Cells[ColHora].Value as string ?? "";
        p.Cloro = row.Cells[ColCloro].Value as string ?? "";
        p.Sabor = row.Cells[ColSabor].Value is true;
        p.Olor = row.Cells[ColOlor].Value is true;
        p.Color = row.Cells[ColColor].Value is true;
        p.Turbidez = row.Cells[ColTurbidez].Value is true;
        p.Comentario = row.Cells[ColComentario].Value as string ?? "";
    }

    private static string AhoraTexto() => AguaServicio.Hora(new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, 0));

    // Se elige el punto y la fila nueva (de hoy, a la hora de ahora, todo "Sí") abre el teclado del cloro
    private void NuevaToma()
    {
        grid.EndEdit();
        var esteMes = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        if (mes != esteMes)
        {
            mes = esteMes;   // las filas nuevas se conservan al cambiar de mes
            Cargar(recargarTomas: false);
        }
        int? filtro = (cmbPunto.SelectedItem as OpcionPunto)?.Id;
        var panel = new ElegirOpcion("Punto de agua", tomas.Select(t => (t.Id.ToString(), t.Nombre, true)).ToList(), filtro?.ToString());
        panel.Abrir(this, btnNueva.PointToScreen(new Point(0, btnNueva.Height)), () =>
        {
            if (panel.Elegida is not string clave) return;
            AnadirYPedirCloro(new Pendiente { Fecha = DateTime.Today, TomaId = int.Parse(clave), Hora = AhoraTexto() });
        });
    }

    // Otra toma del mismo punto y el mismo día; si es de hoy, a la hora de ahora
    private void Repetir(ControlAguaConToma toma)
    {
        if (repetidas.Contains(toma.Control.Id)) return;
        if (nuevas.Any(n => n.RepiteId == toma.Control.Id)) return;   // ya se está repitiendo
        AnadirYPedirCloro(new Pendiente
        {
            Fecha = toma.Control.Fecha.Date,
            TomaId = toma.Toma.Id,
            Hora = toma.Control.Fecha.Date == DateTime.Today ? AhoraTexto() : "",
            RepiteId = toma.Control.Id
        });
    }

    // Con muchas filas, que se siga viendo la nueva; y se abre el teclado del cloro
    private void AnadirYPedirCloro(Pendiente p)
    {
        nuevas.Add(p);
        Mostrar();
        if (btnNueva.Parent is ScrollableControl zona) zona.ScrollControlIntoView(btnNueva);
        var fila = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => r.Tag == p);
        if (fila != null) PedirCloro(fila);
    }

    private Point DebajoDe(DataGridViewRow fila, string columna)
    {
        var celda = grid.GetCellDisplayRectangle(grid.Columns[columna]!.Index, fila.Index, false);
        return grid.PointToScreen(new Point(celda.Left, celda.Bottom));
    }

    // En una fila nueva: el punto y el cloro abren su panel, Sí / No cambia, ✕ la quita
    private void PulsarNueva(DataGridViewRow fila, string columna)
    {
        if (fila.Tag is not Pendiente p || fila.DataGridView == null) return;
        if (columna == ColToma && p.RepiteId == null) ElegirPunto(fila, p);
        else if (columna == ColCloro) PedirCloro(fila);
        else if (ColumnasSiNo.Contains(columna)) fila.Cells[columna].Value = fila.Cells[columna].Value is not true;
        else if (columna is ColHora or ColComentario)
        {
            grid.CurrentCell = fila.Cells[columna];
            grid.BeginEdit(true);
        }
        else if (columna == ColRepetir) QuitarNueva(p);
    }

    private void ElegirPunto(DataGridViewRow fila, Pendiente p)
    {
        var panel = new ElegirOpcion("Punto de agua", tomas.Select(t => (t.Id.ToString(), t.Nombre, true)).ToList(), p.TomaId.ToString());
        panel.Abrir(this, DebajoDe(fila, ColToma), () =>
        {
            if (fila.DataGridView == null || panel.Elegida is not string clave) return;
            p.TomaId = int.Parse(clave);
            fila.Cells[ColToma].Value = NombrePunto(p.TomaId);
        });
    }

    private void PedirCloro(DataGridViewRow fila)
    {
        string actual = fila.Cells[ColCloro].Value as string ?? "";
        var panel = new TecladoNumerico($"Cloro (ppm) · {fila.Cells[ColToma].Value}", actual, largoMaximo: 4);
        panel.Abrir(this, DebajoDe(fila, ColCloro), () =>
        {
            if (fila.DataGridView != null && panel.Valor is string valor && valor != actual)
                fila.Cells[ColCloro].Value = valor;
        });
    }

    private void QuitarNueva(Pendiente p)
    {
        if (!Mensajes.Confirmar(this, "Quitar fila", "¿Quitar esta toma, que aún no está guardada?")) return;
        nuevas.Remove(p);
        Mostrar();
    }

    // Comprueba todas antes de guardar ninguna. Las que se guardan dejan de ser nuevas.
    private void Guardar()
    {
        grid.EndEdit();
        var listas = new List<(Pendiente Fila, ControlAgua Control)>();
        foreach (var p in nuevas)
        {
            string cual = $"La toma nueva de {NombrePunto(p.TomaId)}";
            if (!AguaServicio.LeerHora(p.Hora, out var hora))
            {
                Mensajes.Aviso(this, "No se puede guardar", $"{cual} tiene una hora que no vale. Escríbela así: 9:30.");
                return;
            }
            decimal? cloro = null;
            string texto = p.Cloro.Trim().Replace('.', ',');
            if (texto != "")
            {
                if (!decimal.TryParse(texto, System.Globalization.NumberStyles.Number, Componentes.Es, out var valor))
                {
                    Mensajes.Aviso(this, "No se puede guardar", $"{cual}: el cloro tiene que ser un número (por ejemplo 0,55).");
                    return;
                }
                cloro = valor;
            }
            listas.Add((p, new ControlAgua(0, p.Fecha, p.TomaId, cloro, p.Sabor, p.Olor, p.Color, p.Turbidez, p.Comentario,
                Hora: hora, RepiteId: p.RepiteId)));
        }

        foreach (var (fila, control) in listas)
        {
            try
            {
                servicio.Guardar(control);
                nuevas.Remove(fila);
            }
            catch (ReglaNegocioException ex)
            {
                Mensajes.Aviso(this, "No se puede guardar", ex.Message);
                break;
            }
            catch (DbException ex)
            {
                Mensajes.ErrorBaseDatos(this, "No se pudo guardar la toma de agua.", ex);
                break;
            }
        }
        Cargar(recargarTomas: false);
    }

    private bool DescartarNuevas()
    {
        grid.EndEdit();
        if (nuevas.Count == 0) return true;
        if (!Mensajes.DescartarCambios(this, "Hay tomas nuevas sin guardar en el control de agua. ¿Descartarlas?")) return false;
        nuevas.Clear();
        return true;
    }

    private void AvisarAlCerrar(object? sender, FormClosingEventArgs e)
    {
        if (!e.Cancel && !DescartarNuevas()) e.Cancel = true;
    }

    // Corregir o borrar una toma ya guardada, en la ventana. Si se borra una que repite una fila nueva, la nueva pasa a repetir la anterior.
    private void Corregir(DataGridViewRow fila)
    {
        if (fila.Tag is not ControlAguaConToma toma) return;
        using var ventana = new FormTomaAgua(toma.Control, tomas, toma.Toma);
        if (ventana.ShowDialog(this) != DialogResult.OK) return;
        Cargar(recargarTomas: false);
        if (controles.Any(c => c.Control.Id == toma.Control.Id)) return;   // no se ha borrado
        foreach (var p in nuevas.Where(p => p.RepiteId == toma.Control.Id))
            p.RepiteId = toma.Control.RepiteId;
    }
}
