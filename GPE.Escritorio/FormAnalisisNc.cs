using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Análisis de las no conformidades: cuántas hay de cada sección, MC/PCC/BPM, tipo, operario u origen
// en un mes o un año. Al pulsar una barra salen debajo las de ese grupo; con doble clic se abre la ficha.
public class FormAnalisisNc : Form
{
    private readonly NoConformidadServicio servicio = new();
    private List<NoConformidad> registros;
    private Dictionary<string, string> nombresOrigen = new();
    private DateTime periodo;

    private readonly ComboBox cmbVer = new();
    private readonly Label lblPeriodo = new();
    private readonly Button btnSiguiente;
    private readonly ComboBox cmbContar = new();
    private readonly ComboBox cmbAgrupar = new();
    private readonly Label lblResumen = new();
    private readonly Label lblNota = new();
    private readonly GraficaBarras grafica = new();
    private readonly Panel zonaGrafica = new();
    private readonly Label lblLista = new();
    private readonly DataGridView grid = new();

    private const string VerMes = "Mes", VerAno = "Año";
    private const string SinIndicar = "(sin indicar)";
    private const int ColTipo = 2, ColEstado = 5;

    // Qué registros se cuentan. Las acciones de mejora no son problemas: no se cuentan.
    private record OpcionContar(string Nombre, string[] Tipos)
    {
        public override string ToString() => Nombre;
    }
    private static readonly OpcionContar[] OpcionesContar =
    {
        new("NC e incidencias", new[] { TipoRegistro.NoConformidad, TipoRegistro.Incidencia }),
        new("No conformidades", new[] { TipoRegistro.NoConformidad }),
        new("Incidencias", new[] { TipoRegistro.Incidencia })
    };

    // Por qué se agrupa; Nota: aviso si ese dato solo se rellena en un tipo de registro
    private record OpcionAgrupar(string Nombre, Func<NoConformidad, string?> Valor, string? SoloEn)
    {
        public override string ToString() => Nombre;
    }
    private readonly OpcionAgrupar[] opcionesAgrupar;

    // Hay que recargar la lista de no conformidades al cerrar (se ha guardado alguna ficha)
    public bool HayCambios { get; private set; }

    public FormAnalisisNc(List<NoConformidad> registros)
    {
        this.registros = registros;
        // Empieza en el mes de la última detectada (o el actual si no hay ninguna)
        var ultima = registros.Count == 0 ? DateTime.Today : registros.Max(r => r.FechaDetectado);
        periodo = new DateTime(ultima.Year, ultima.Month, 1);

        opcionesAgrupar = new OpcionAgrupar[]
        {
            new("Sección", nc => nc.Seccion, null),
            new("MC/PCC/BPM", nc => nc.McPccBpm, TipoRegistro.Incidencia),
            new("Tipo", nc => nc.Tipo, TipoRegistro.NoConformidad),
            new("Operario", nc => nc.Operario, TipoRegistro.Incidencia),
            new("Origen", nc => nc.Origen == null ? null : nombresOrigen.GetValueOrDefault(nc.Origen, nc.Origen), null)
        };

        Text = "Análisis de no conformidades";
        Size = new Size(1250, 820);
        MinimumSize = new Size(950, 600);
        StartPosition = FormStartPosition.CenterParent;
        Componentes.AjustarAPantalla(this);
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        btnSiguiente = Flecha("▶", 1);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 6,
            Padding = new Padding(24, 14, 24, 14)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 55));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 45));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Cuántos problemas hay de cada grupo, de más a menos. Pulsa una barra para ver cuáles son; " +
                   "con doble clic en uno se abre su ficha.",
            AutoSize = true,
            ForeColor = Tema.Suave,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);
        layout.Controls.Add(CrearBarra(), 0, 1);
        layout.Controls.Add(CrearZonaGrafica(), 0, 2);

        lblLista.AutoSize = true;
        lblLista.Font = Tema.FuenteNegrita;
        lblLista.ForeColor = Tema.Texto;
        lblLista.Margin = new Padding(0, 12, 0, 6);
        layout.Controls.Add(lblLista, 0, 3);
        layout.Controls.Add(ConfigurarGrid(), 0, 4);

        var btnCerrar = Tema.BotonSecundario("Cerrar", 130);
        btnCerrar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCerrar;
        var acciones = Componentes.Acciones(btnCerrar);
        acciones.Margin = new Padding(0, 10, 0, 0);
        layout.Controls.Add(acciones, 0, 5);

        Controls.Add(layout);
        Load += (s, e) =>
        {
            CargarOrigenes();
            Mostrar();
        };
    }

    private Control CrearBarra()
    {
        var barra = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = false,
            BackColor = Color.White,
            Padding = new Padding(15, 8, 15, 8),
            Margin = new Padding(0, 0, 0, 10)
        };

        cmbVer.Width = 90;
        cmbVer.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbVer.Items.AddRange(new object[] { VerMes, VerAno });
        cmbVer.SelectedItem = VerMes;
        cmbVer.SelectedIndexChanged += (s, e) =>
        {
            // De año a mes: el último mes de ese año que ya haya llegado
            if (cmbVer.Text == VerMes)
                periodo = periodo.Year < DateTime.Today.Year
                    ? new DateTime(periodo.Year, 12, 1)
                    : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            Mostrar();
        };

        lblPeriodo.AutoSize = false;
        lblPeriodo.Size = new Size(170, 40);
        lblPeriodo.TextAlign = ContentAlignment.MiddleCenter;
        lblPeriodo.Font = Tema.FuenteNegrita;
        lblPeriodo.Margin = new Padding(0, 18, 6, 0);

        cmbContar.Width = 180;
        cmbContar.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbContar.Items.AddRange(OpcionesContar);
        cmbContar.SelectedIndex = 0;
        cmbContar.SelectedIndexChanged += (s, e) => Mostrar();

        cmbAgrupar.Width = 160;
        cmbAgrupar.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbAgrupar.Items.AddRange(opcionesAgrupar);
        cmbAgrupar.SelectedIndex = 0;
        cmbAgrupar.SelectedIndexChanged += (s, e) => Mostrar();

        lblResumen.AutoSize = true;
        lblResumen.ForeColor = Tema.Suave;
        lblResumen.Margin = new Padding(20, 28, 0, 0);

        var campoVer = Componentes.CampoFiltro("Ver por", cmbVer);
        campoVer.Margin = new Padding(0, 0, 12, 0);
        barra.Controls.Add(campoVer);
        barra.Controls.Add(Flecha("◀", -1));
        barra.Controls.Add(lblPeriodo);
        barra.Controls.Add(btnSiguiente);
        var campoContar = Componentes.CampoFiltro("Qué contar", cmbContar);
        campoContar.Margin = new Padding(24, 0, 0, 0);
        barra.Controls.Add(campoContar);
        barra.Controls.Add(Componentes.CampoFiltro("Agrupar por", cmbAgrupar));
        barra.Controls.Add(lblResumen);
        return barra;
    }

    private Button Flecha(string texto, int pasos)
    {
        var boton = Tema.BotonSecundario(texto, 48);
        boton.Height = 40;
        boton.Margin = new Padding(0, 18, 6, 0);
        boton.Click += (s, e) =>
        {
            var nuevo = cmbVer.Text == VerAno ? periodo.AddYears(pasos) : periodo.AddMonths(pasos);
            if (nuevo > DateTime.Today) return;
            periodo = nuevo;
            Mostrar();
        };
        return boton;
    }

    // La gráfica se desplaza si hay muchos grupos (por ejemplo, operarios); la nota va encima
    private Control CrearZonaGrafica()
    {
        var marco = new Panel { Dock = DockStyle.Fill, BackColor = Color.White, Margin = new Padding(0) };

        zonaGrafica.Dock = DockStyle.Fill;
        zonaGrafica.AutoScroll = true;
        zonaGrafica.BackColor = Color.White;
        zonaGrafica.Resize += (s, e) => AjustarGrafica();
        grafica.Location = new Point(0, 0);
        grafica.GrupoPulsado += grupo => MostrarLista();
        zonaGrafica.Controls.Add(grafica);

        lblNota.Dock = DockStyle.Top;
        lblNota.Height = 30;
        lblNota.Padding = new Padding(15, 0, 0, 0);
        lblNota.TextAlign = ContentAlignment.MiddleLeft;
        lblNota.BackColor = Tema.AvisoFondo;
        lblNota.ForeColor = Tema.Aviso;
        lblNota.Visible = false;

        marco.Controls.Add(zonaGrafica);
        marco.Controls.Add(lblNota);
        return marco;
    }

    private void AjustarGrafica()
    {
        int ancho = zonaGrafica.ClientSize.Width;
        int alto = Math.Max(grafica.AltoNecesario, zonaGrafica.ClientSize.Height);
        // Con barra de desplazamiento el ancho útil es algo menor
        if (grafica.AltoNecesario > zonaGrafica.ClientSize.Height) ancho -= SystemInformation.VerticalScrollBarWidth;
        grafica.Size = new Size(Math.Max(ancho, 300), alto);
    }

    private Control ConfigurarGrid()
    {
        Componentes.EstiloTabla(grid, altoFila: 40);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.MultiSelect = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.ShowCellToolTips = true;
        grid.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;
        grid.DefaultCellStyle.SelectionForeColor = Tema.Texto;

        (string Nombre, int Ancho)[] columnas =
        {
            ("Nº", 80), ("DETECTADA", 105), ("TIPO", 115), ("SECCIÓN", 150), ("PROBLEMA", 0), ("ESTADO", 140)
        };
        foreach (var (nombre, ancho) in columnas)
        {
            int i = grid.Columns.Add(nombre, nombre);
            grid.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
            if (ancho == 0) continue;
            grid.Columns[i].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns[i].Width = ancho;
        }
        grid.Columns[0].DefaultCellStyle.ForeColor = Tema.Acento;
        grid.Columns[0].DefaultCellStyle.Font = Tema.FuenteNegrita;

        grid.CellPainting += (s, e) =>
        {
            if (e.RowIndex < 0 || grid.Rows[e.RowIndex].Tag is not NoConformidad nc) return;
            if (e.ColumnIndex == ColTipo)
            {
                var (fondo, texto) = Tema.ColorTipoRegistro(nc.TipoRegistro);
                Componentes.PintarEtiqueta(e, nc.TipoRegistro, fondo, texto);
            }
            else if (e.ColumnIndex == ColEstado)
            {
                string estado = NoConformidadServicio.Estado(nc);
                var (fondo, texto) = Tema.ColorEstadoRegistro(estado);
                Componentes.PintarEtiqueta(e, estado, fondo, texto);
            }
        };
        grid.CellDoubleClick += (s, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].Tag is NoConformidad nc) AbrirFicha(nc);
        };
        grid.KeyDown += (s, e) =>
        {
            if (e.KeyCode != Keys.Enter || grid.CurrentRow?.Tag is not NoConformidad nc) return;
            e.Handled = true;
            AbrirFicha(nc);
        };
        return grid;
    }

    // Datos

    // "I · Interna": el origen solo con su letra no se entiende
    private void CargarOrigenes()
    {
        try
        {
            var origenes = servicio.ObtenerListas().GetValueOrDefault(ListaCalidad.Origen) ?? new();
            nombresOrigen = origenes.ToDictionary(o => o.Valor, o => o.ToString());
        }
        catch (DbException)
        {
            nombresOrigen = new();   // se ve solo la letra
        }
    }

    private (DateTime Desde, DateTime Hasta) Rango() => cmbVer.Text == VerAno
        ? (new DateTime(periodo.Year, 1, 1), new DateTime(periodo.Year, 12, 31))
        : (periodo, periodo.AddMonths(1).AddDays(-1));

    private List<NoConformidad> DelPeriodo()
    {
        var (desde, hasta) = Rango();
        var tipos = ((OpcionContar)cmbContar.SelectedItem!).Tipos;
        return registros
            .Where(r => tipos.Contains(r.TipoRegistro) && r.FechaDetectado >= desde && r.FechaDetectado <= hasta)
            .ToList();
    }

    private string Grupo(NoConformidad nc)
    {
        string? valor = ((OpcionAgrupar)cmbAgrupar.SelectedItem!).Valor(nc);
        return string.IsNullOrWhiteSpace(valor) ? SinIndicar : valor.Trim();
    }

    private void Mostrar()
    {
        if (cmbVer.Text == VerAno) periodo = new DateTime(periodo.Year, 1, 1);
        string nombre = cmbVer.Text == VerAno ? periodo.Year.ToString() : periodo.ToString("MMMM yyyy", Componentes.Es);
        lblPeriodo.Text = char.ToUpper(nombre[0], Componentes.Es) + nombre[1..];
        btnSiguiente.Enabled = (cmbVer.Text == VerAno ? periodo.AddYears(1) : periodo.AddMonths(1)) <= DateTime.Today;

        var lista = DelPeriodo();
        // De más a menos; "(sin indicar)" siempre al final, en gris
        var grupos = lista
            .GroupBy(Grupo)
            .Select(g => new GraficaBarras.Barra(g.Key, g.Count(), g.Key == SinIndicar))
            .OrderBy(b => b.Apagada).ThenByDescending(b => b.Valor).ThenBy(b => b.Nombre)
            .ToList();
        grafica.Mostrar(grupos, "No hay problemas registrados en este periodo.");
        AjustarGrafica();

        lblResumen.Text = lista.Count == 0 ? "" : $"{lista.Count} {(lista.Count == 1 ? "registro" : "registros")}";

        // Avisa cuando se agrupa por un dato que el tipo elegido no rellena
        var agrupar = (OpcionAgrupar)cmbAgrupar.SelectedItem!;
        var contar = (OpcionContar)cmbContar.SelectedItem!;
        lblNota.Visible = agrupar.SoloEn != null && contar.Tipos.Any(t => t != agrupar.SoloEn);
        string enLas = agrupar.SoloEn == TipoRegistro.NoConformidad ? "no conformidades" : "incidencias";
        lblNota.Text = $"{agrupar.Nombre} solo se rellena en las {enLas}: las demás salen como \"{SinIndicar}\".";

        MostrarLista();
    }

    // Las del grupo pulsado, o todas las del periodo si no hay ninguno pulsado
    private void MostrarLista()
    {
        string? grupo = grafica.Elegido;
        var visibles = DelPeriodo()
            .Where(nc => grupo == null || Grupo(nc) == grupo)
            .OrderByDescending(nc => nc.FechaDetectado).ThenByDescending(nc => nc.Id)
            .ToList();

        lblLista.Text = grupo == null
            ? $"Todos los registros del periodo ({visibles.Count})  ·  pulsa una barra para ver solo ese grupo"
            : $"{grupo} ({visibles.Count})  ·  pulsa otra vez la barra para ver todos";

        grid.Rows.Clear();
        foreach (var nc in visibles)
        {
            int i = grid.Rows.Add(
                nc.Numero ?? "—",
                nc.FechaDetectado.ToString("dd/MM/yy"),
                nc.TipoRegistro,
                nc.Seccion ?? "—",
                nc.Problema.Split('\n')[0].Trim(),
                NoConformidadServicio.Estado(nc));
            grid.Rows[i].Tag = nc;
            grid.Rows[i].Cells[4].ToolTipText = nc.Problema.Length > 600 ? nc.Problema[..600] + "…" : nc.Problema;
        }
        grid.ClearSelection();
    }

    // Igual que desde la lista: se vuelve a leer por si otro la ha cambiado
    private void AbrirFicha(NoConformidad nc)
    {
        NoConformidad? actual;
        try
        {
            actual = servicio.ObtenerRegistro(nc.Id);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo cargar el registro.", ex);
            return;
        }
        if (actual == null)
        {
            Mensajes.Informacion(this, "Registro no encontrado", "Este registro ya no existe: puede que otro usuario lo haya borrado.");
            return;
        }

        using (var formulario = new FormNoConformidad(actual))
            if (formulario.ShowDialog(this) != DialogResult.OK) return;

        HayCambios = true;
        try
        {
            registros = servicio.ObtenerRegistros();
        }
        catch (DbException)
        {
            return;
        }
        Mostrar();
    }
}

// Barras horizontales: el nombre a la izquierda, la barra y "12 · 35 %" al final.
// Se pulsa una barra para elegirla (las demás se apagan) y otra vez para quitarla.
public class GraficaBarras : Control
{
    public record Barra(string Nombre, int Valor, bool Apagada);

    private List<Barra> barras = new();
    private string textoVacio = "";
    private int? encima;

    private const int AltoBarra = 34, MargenArriba = 12, AnchoNombre = 300, AnchoCifra = 200;

    public string? Elegido { get; private set; }
    public event Action<string?>? GrupoPulsado;

    public int AltoNecesario => MargenArriba * 2 + barras.Count * AltoBarra;

    public GraficaBarras()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Font = Tema.Fuente;
    }

    public void Mostrar(List<Barra> barras, string textoVacio)
    {
        this.barras = barras;
        this.textoVacio = textoVacio;
        // Si el grupo elegido ya no sale (otro periodo u otra agrupación), se quita
        if (Elegido != null && !barras.Any(b => b.Nombre == Elegido)) Elegido = null;
        Invalidate();
    }

    private int? BarraEn(Point p)
    {
        int i = (p.Y - MargenArriba) / AltoBarra;
        return p.Y >= MargenArriba && i >= 0 && i < barras.Count ? i : null;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;

        if (barras.Count == 0)
        {
            TextRenderer.DrawText(g, textoVacio, Tema.Fuente, new Rectangle(0, 0, Width, Parent?.ClientSize.Height ?? Height),
                Tema.Suave, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        // Las apagadas ("sin indicar") no cuentan para la escala ni para el %: si no, cuando hay muchas
        // sin el dato, las de verdad salen diminutas
        var conDato = barras.Where(b => !b.Apagada).ToList();
        int maximo = conDato.Count > 0 ? conDato.Max(b => b.Valor) : barras.Max(b => b.Valor);
        int totalConDato = conDato.Sum(b => b.Valor);
        int inicioBarra = AnchoNombre + 30;
        int anchoBarras = Math.Max(50, Width - inicioBarra - AnchoCifra - 20);

        for (int i = 0; i < barras.Count; i++)
        {
            var barra = barras[i];
            int y = MargenArriba + i * AltoBarra;
            bool elegida = barra.Nombre == Elegido;
            bool apagada = Elegido != null && !elegida;

            if (elegida || encima == i)
                using (var fondo = new SolidBrush(elegida ? Tema.AcentoSuave : Tema.FondoSoloLectura))
                    g.FillRectangle(fondo, 0, y, Width, AltoBarra);

            TextRenderer.DrawText(g, barra.Nombre, elegida ? Tema.FuenteNegrita : Font,
                new Rectangle(15, y, AnchoNombre, AltoBarra), barra.Apagada ? Tema.Suave : Tema.Texto,
                TextFormatFlags.Right | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);

            int largo = Math.Clamp((int)(anchoBarras * (barra.Valor / (double)maximo)), 3, anchoBarras);
            Color color = barra.Apagada ? Tema.Borde : Tema.Acento;
            if (apagada) color = Color.FromArgb(70, color);
            using (var relleno = new SolidBrush(color))
                g.FillRectangle(relleno, inicioBarra, y + 7, largo, AltoBarra - 14);

            string cifra = barra.Apagada || totalConDato == 0
                ? $"{barra.Valor}  ·  no cuentan en el %"
                : $"{barra.Valor}  ·  {barra.Valor * 100.0 / totalConDato:0} %";
            TextRenderer.DrawText(g, cifra, Tema.FuenteNegrita, new Rectangle(inicioBarra + largo + 8, y, AnchoCifra, AltoBarra),
                apagada ? Tema.Suave : Tema.Texto, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var i = BarraEn(e.Location);
        Cursor = i == null ? Cursors.Default : Cursors.Hand;
        if (i == encima) return;
        encima = i;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        encima = null;
        Invalidate();
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (BarraEn(e.Location) is not int i) return;
        Elegido = barras[i].Nombre == Elegido ? null : barras[i].Nombre;
        Invalidate();
        GrupoPulsado?.Invoke(Elegido);
    }
}
