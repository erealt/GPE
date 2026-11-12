using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Control de agua (hoja AGUA, MT-RG09): las tomas de un mes, la más reciente arriba.
// No se hacen todos los días: cada toma se apunta suelta con "+ Nueva toma".
// Si una no convence se repite: la nueva queda como "repetición" y la otra como "repetida".
public class ModuloControlAgua : UserControl
{
    private readonly AguaServicio servicio = new();
    private List<AguaToma> tomas = new();
    private DateTime mes = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    private List<ControlAguaConToma> controles = new();
    private HashSet<int> repetidas = new();   // ids de las tomas que alguna otra repite

    private readonly Label lblMes = new();
    private readonly Button btnSiguiente;
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
        Load += (s, e) => Cargar(recargarTomas: true);
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
            Text = "Pulsa \"+ Nueva toma\" para apuntar una. Doble clic en una toma para corregirla o borrarla. " +
                   "Si el resultado no convence, \"Repetir\" apunta otra del mismo punto y la primera se queda como repetida.",
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

        var btnNueva = Tema.BotonPrincipal("+  Nueva toma", 180);
        btnNueva.Height = 46;
        btnNueva.Anchor = AnchorStyles.Top | AnchorStyles.Right;
        btnNueva.Margin = new Padding(0, 10, 0, 0);
        btnNueva.Click += (s, e) => NuevaToma();

        cabecera.Controls.Add(titulos, 0, 0);
        cabecera.Controls.Add(btnNueva, 1, 0);
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
        btnRecargar.Click += (s, e) => Cargar(recargarTomas: true);

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
        var zona = new Panel { Dock = DockStyle.Fill };

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
        Componentes.EstiloTabla(grid, altoFila: 44);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
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
        grid.CellClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && e.ColumnIndex >= 0 && grid.Columns[e.ColumnIndex].Name == ColRepetir)
                Repetir(grid.Rows[e.RowIndex]);
        };
        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && (e.ColumnIndex < 0 || grid.Columns[e.ColumnIndex].Name != ColRepetir))
                Corregir(grid.Rows[e.RowIndex]);
        };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || grid.CurrentRow == null) return;
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
        if (fila.Tag is not ControlAguaConToma toma) return;
        string columna = grid.Columns[e.ColumnIndex].Name;

        if (ColumnasSiNo.Contains(columna))
        {
            bool si = e.Value is true;
            var (fondo, texto) = Tema.ColorEstadoCasilla(si ? EstadoCasilla.Correcto : EstadoCasilla.NoCorrecto);
            Componentes.PintarEtiqueta(e, si ? "✓  Sí" : "✗  No", fondo, texto);
        }
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
        {
            e.PaintBackground(e.CellBounds, true);
            if (!EsRepetida(fila))
            {
                var caja = Rectangle.Inflate(e.CellBounds, -10, -8);
                using (var borde = new Pen(Tema.LineaTabla))
                    e.Graphics.DrawRectangle(borde, caja);
                TextRenderer.DrawText(e.Graphics, "↻  Repetir", Tema.FuentePequenaNegrita, caja, Tema.Acento,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
            e.Handled = true;
        }
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

    // La más reciente arriba
    private void Mostrar()
    {
        int? punto = (cmbPunto.SelectedItem as OpcionPunto)?.Id;
        var visibles = controles
            .Where(c => punto == null || c.Toma.Id == punto)
            .OrderByDescending(c => c.Control.Fecha).ThenByDescending(c => c.Control.Hora).ThenByDescending(c => c.Control.Id)
            .ToList();

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
            row.Cells[ColToma].ToolTipText = $"Apuntada por {c.CreadoPor}";
            if (c.Comentario != null) row.Cells[ColComentario].ToolTipText = c.Comentario;
        }
        grid.ClearSelection();

        int repetidasVisibles = visibles.Count(c => repetidas.Contains(c.Control.Id));
        int fuera = visibles.Count(c => !repetidas.Contains(c.Control.Id) && AguaServicio.FueraDeRango(c.Control.Cloro, c.Toma));
        lblResumen.Text = visibles.Count == 0 ? "" : string.Join("  ·  ", new[]
        {
            $"{visibles.Count} {(visibles.Count == 1 ? "toma" : "tomas")}",
            repetidasVisibles == 0 ? null : $"{repetidasVisibles} {(repetidasVisibles == 1 ? "repetida" : "repetidas")}",
            fuera == 0 ? null : $"{fuera} fuera del límite"
        }.OfType<string>());
        lblResumen.ForeColor = fuera > 0 ? Tema.Error : Tema.Suave;

        lblVacio.Text = punto == null ? "No hay tomas de agua apuntadas en este mes." : "Este punto no tiene tomas en este mes.";
        lblVacio.Visible = visibles.Count == 0;
        grid.Visible = visibles.Count > 0;
    }

    // Tomas

    // Con un punto elegido en el filtro, la nueva empieza con ese punto
    private void NuevaToma()
    {
        ControlAgua? nueva = null;
        if ((cmbPunto.SelectedItem as OpcionPunto)?.Id is int punto && tomas.Any(t => t.Id == punto))
            nueva = new ControlAgua(0, DateTime.Today, punto, null, true, true, true, true, null,
                Hora: new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, 0));
        using var ventana = new FormTomaAgua(nueva, tomas);
        if (ventana.ShowDialog(this) == DialogResult.OK && ventana.Guardada is ControlAgua guardada) IrAlMes(guardada.Fecha);
    }

    private void Corregir(DataGridViewRow fila)
    {
        if (fila.Tag is not ControlAguaConToma toma) return;
        using var ventana = new FormTomaAgua(toma.Control, tomas, toma.Toma);
        if (ventana.ShowDialog(this) == DialogResult.OK) Cargar(recargarTomas: false);
    }

    // Otra toma del mismo punto y el mismo día; si es de hoy, a la hora de ahora
    private void Repetir(DataGridViewRow fila)
    {
        if (fila.Tag is not ControlAguaConToma toma || EsRepetida(fila)) return;
        TimeSpan? hora = toma.Control.Fecha.Date == DateTime.Today
            ? new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, 0) : null;
        using var ventana = new FormTomaAgua(AguaServicio.Repeticion(toma.Control, hora), tomas, toma.Toma, toma.Control);
        if (ventana.ShowDialog(this) == DialogResult.OK) Cargar(recargarTomas: false);
    }

    // Después de apuntar una toma se enseña su mes (normalmente el actual)
    private void IrAlMes(DateTime dia)
    {
        mes = new DateTime(dia.Year, dia.Month, 1);
        Cargar(recargarTomas: false);
    }
}
