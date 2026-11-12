using GPE.Datos;
using GPE.Logica;
using System.Data.Common;
using System.Drawing.Drawing2D;

namespace GPE.Escritorio;

// Gráfica del cloro (ppm) de un mes, de un punto de agua o de todos. Solo consulta.
public class FormAguaGrafica : Form
{
    private readonly AguaServicio servicio = new();
    private readonly List<AguaToma> tomas;
    private DateTime mes;
    private List<ControlAguaConToma> controles = new();

    private readonly Label lblMes = new();
    private readonly Button btnSiguiente;
    private readonly ComboBox cmbPunto = new();
    private readonly Label lblResumen = new();
    private readonly GraficaCloro grafica = new();

    // Opción del filtro de punto: null = todos
    private record OpcionPunto(int? Id, string Nombre)
    {
        public override string ToString() => Nombre;
    }

    // tomas: los puntos activos, para poder elegir también uno que no tenga controles este mes
    public FormAguaGrafica(DateTime dia, List<AguaToma> tomas)
    {
        this.tomas = tomas;
        mes = new DateTime(dia.Year, dia.Month, 1);

        Text = "Gráfica del cloro";
        Size = new Size(1250, 760);
        MinimumSize = new Size(950, 550);
        StartPosition = FormStartPosition.CenterParent;
        Componentes.AjustarAPantalla(this);
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        btnSiguiente = Flecha("▶", 1);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24, 14, 24, 14)
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        layout.Controls.Add(new Label
        {
            Text = "Cloro de cada control del mes. En rojo, los que se salen del límite. Pasa el ratón por un punto para ver el dato.",
            AutoSize = true,
            ForeColor = Tema.Suave,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);
        layout.Controls.Add(CrearBarra(), 0, 1);

        grafica.Dock = DockStyle.Fill;
        layout.Controls.Add(grafica, 0, 2);

        var btnCerrar = Tema.BotonSecundario("Cerrar", 130);
        btnCerrar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCerrar;
        var acciones = Componentes.Acciones(btnCerrar);
        acciones.Margin = new Padding(0, 10, 0, 0);
        layout.Controls.Add(acciones, 0, 3);

        Controls.Add(layout);
        Load += (s, e) => Cargar();
    }

    // Igual que la barra de la pantalla de control de agua, pero con el mes
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

        lblMes.AutoSize = false;
        lblMes.Size = new Size(170, 40);
        lblMes.TextAlign = ContentAlignment.MiddleCenter;
        lblMes.Font = Tema.FuenteNegrita;
        lblMes.Margin = new Padding(0, 18, 6, 0);

        var btnEsteMes = Tema.BotonSecundario("Este mes", 100);
        btnEsteMes.Height = 40;
        btnEsteMes.Margin = new Padding(0, 18, 30, 0);
        btnEsteMes.Click += (s, e) => CambiarMes(new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1));

        cmbPunto.Width = 320;
        cmbPunto.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbPunto.SelectedIndexChanged += (s, e) => Mostrar();

        lblResumen.AutoSize = true;
        lblResumen.ForeColor = Tema.Suave;
        lblResumen.Margin = new Padding(20, 28, 0, 0);

        barra.Controls.Add(Flecha("◀", -1));
        barra.Controls.Add(lblMes);
        barra.Controls.Add(btnSiguiente);
        barra.Controls.Add(btnEsteMes);
        barra.Controls.Add(Componentes.CampoFiltro("Punto de agua", cmbPunto));
        barra.Controls.Add(lblResumen);
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

    // Datos

    private void CambiarMes(DateTime nuevo)
    {
        if (nuevo > DateTime.Today || nuevo == mes) return;
        mes = nuevo;
        Cargar();
    }

    private void Cargar()
    {
        try
        {
            controles = servicio.ObtenerMes(mes);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo cargar el control de agua del mes.", ex);
            controles = new();
        }

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

    private void Mostrar()
    {
        int? punto = (cmbPunto.SelectedItem as OpcionPunto)?.Id;
        var conCloro = controles
            .Where(c => c.Control.Cloro != null && (punto == null || c.Toma.Id == punto))
            .ToList();

        // Una línea por punto, cada una de un color, en el orden de la lista de puntos
        var orden = tomas.Select(t => t.Id).ToList();
        var series = conCloro
            .GroupBy(c => c.Toma.Id)
            .OrderBy(g => orden.IndexOf(g.Key) is int i && i >= 0 ? i : int.MaxValue)
            .Select((g, i) => new GraficaCloro.Serie(
                g.First().Toma.Nombre,
                punto == null ? GraficaCloro.ColorSerie(i) : Tema.Acento,
                g.Select(c => new GraficaCloro.Medida(c.Control.Fecha, c.Control.Cloro!.Value,
                    AguaServicio.FueraDeRango(c.Control.Cloro, c.Toma), c.Control.Hora)).ToList()))
            .ToList();

        // El límite se dibuja si todos los puntos que salen tienen el mismo
        var puntosVistos = punto == null
            ? tomas.Concat(conCloro.Select(c => c.Toma)).DistinctBy(t => t.Id).ToList()
            : tomas.Concat(controles.Select(c => c.Toma)).Where(t => t.Id == punto).Take(1).ToList();
        decimal? Comun(Func<AguaToma, decimal?> valor) =>
            puntosVistos.Select(valor).Distinct().ToList() is { Count: 1 } unico ? unico[0] : null;

        grafica.Mostrar(mes, series, Comun(t => t.CloroMin), Comun(t => t.CloroMax),
            punto == null ? "No hay controles con cloro en este mes." : "Este punto no tiene controles con cloro en este mes.");

        int total = conCloro.Count;
        int fuera = series.Sum(s => s.Medidas.Count(m => m.FueraDeRango));
        lblResumen.Text = total == 0 ? "" : string.Join("  ·  ", new[]
        {
            $"{total} {(total == 1 ? "control" : "controles")} con cloro",
            fuera == 0 ? null : $"{fuera} fuera del límite"
        }.OfType<string>());
        lblResumen.ForeColor = fuera > 0 ? Tema.Error : Tema.Suave;
    }
}

// La gráfica en sí: días del mes abajo, ppm a la izquierda, una línea por punto de agua
// y el límite como una raya roja discontinua.
public class GraficaCloro : Control
{
    public record Medida(DateTime Fecha, decimal Cloro, bool FueraDeRango, TimeSpan? Hora = null);
    public record Serie(string Nombre, Color Color, List<Medida> Medidas);

    private DateTime mes = DateTime.Today;
    private List<Serie> series = new();
    private decimal? minimo, maximo;
    private string textoVacio = "";

    private readonly ToolTip tooltip = new() { InitialDelay = 0, ReshowDelay = 0 };
    private string? textoTooltip;
    // Dónde quedó dibujada cada medida, para el tooltip
    private readonly List<(PointF Punto, string Texto)> dibujadas = new();

    // Colores de las líneas; ninguno es rojo, que es el de "fuera del límite"
    private static readonly Color[] Colores =
    {
        Color.FromArgb(89, 0, 246), Color.FromArgb(0, 130, 160), Color.FromArgb(220, 130, 0), Color.FromArgb(40, 150, 70),
        Color.FromArgb(150, 60, 170), Color.FromArgb(90, 110, 130), Color.FromArgb(0, 90, 200), Color.FromArgb(160, 120, 40),
        Color.FromArgb(0, 160, 130), Color.FromArgb(200, 80, 150), Color.FromArgb(60, 60, 60)
    };
    public static Color ColorSerie(int i) => Colores[i % Colores.Length];

    private const int MargenIzquierdo = 64, MargenArriba = 24, MargenAbajo = 46, AnchoLeyenda = 340;

    public GraficaCloro()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.White;
        Font = Tema.FuentePequena;
    }

    public void Mostrar(DateTime mes, List<Serie> series, decimal? minimo, decimal? maximo, string textoVacio)
    {
        this.mes = mes;
        this.series = series;
        this.minimo = minimo;
        this.maximo = maximo;
        this.textoVacio = textoVacio;
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        dibujadas.Clear();

        if (series.Count == 0)
        {
            TextRenderer.DrawText(g, textoVacio, Tema.Fuente, ClientRectangle, Tema.Suave,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        bool conLeyenda = series.Count > 1;
        var zona = Rectangle.FromLTRB(MargenIzquierdo, MargenArriba,
            Width - (conLeyenda ? AnchoLeyenda : 24), Height - MargenAbajo);
        if (zona.Width < 100 || zona.Height < 60) return;

        int dias = DateTime.DaysInMonth(mes.Year, mes.Month);
        float anchoDia = zona.Width / (float)dias;
        float X(DateTime fecha) => zona.Left + (fecha.Day - 0.5f) * anchoDia;

        // Eje de ppm: de 0 a algo por encima del valor más alto (o del límite), en pasos redondos
        decimal mayor = Math.Max(series.Max(s => s.Medidas.Max(m => m.Cloro)), Math.Max(maximo ?? 0, minimo ?? 0));
        decimal paso = mayor <= 0.5m ? 0.1m : mayor <= 1m ? 0.2m : mayor <= 2.5m ? 0.5m : 1m;
        decimal tope = Math.Max(paso * 2, Math.Ceiling(mayor * 1.15m / paso) * paso);
        float Y(decimal ppm) => zona.Bottom - (float)(ppm / tope) * zona.Height;

        // Fines de semana, más claros
        using (var finde = new SolidBrush(Tema.FondoSoloLectura))
            for (int d = 1; d <= dias; d++)
                if (new DateTime(mes.Year, mes.Month, d).DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                    g.FillRectangle(finde, zona.Left + (d - 1) * anchoDia, zona.Top, anchoDia, zona.Height);

        // Por encima del máximo y por debajo del mínimo, rojo muy suave
        using (var fondoError = new SolidBrush(Tema.FondoError))
        {
            if (maximo is decimal max && max < tope)
                g.FillRectangle(fondoError, zona.Left, zona.Top, zona.Width, Y(max) - zona.Top);
            if (minimo is decimal min && min > 0)
                g.FillRectangle(fondoError, zona.Left, Y(min), zona.Width, zona.Bottom - Y(min));
        }

        // Rejilla y números de ppm
        using (var rejilla = new Pen(Tema.Linea))
            for (decimal v = 0; v <= tope; v += paso)
            {
                float y = Y(v);
                g.DrawLine(rejilla, zona.Left, y, zona.Right, y);
                var caja = new Rectangle(0, (int)y - 10, MargenIzquierdo - 8, 20);
                TextRenderer.DrawText(g, v.ToString("0.0#", Componentes.Es), Font, caja, Tema.Suave,
                    TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
            }
        TextRenderer.DrawText(g, "ppm", Tema.FuenteEtiqueta, new Rectangle(0, 0, MargenIzquierdo - 8, MargenArriba - 4),
            Tema.Suave, TextFormatFlags.Right | TextFormatFlags.Bottom);

        // Días del mes; si no caben todos, uno sí y otro no
        int cada = anchoDia >= 22 ? 1 : 2;
        for (int d = 1; d <= dias; d += cada)
        {
            var caja = new Rectangle((int)(zona.Left + (d - 1) * anchoDia) - 10, zona.Bottom + 4, (int)anchoDia + 20, 18);
            TextRenderer.DrawText(g, d.ToString(), Font, caja, Tema.Suave, TextFormatFlags.HorizontalCenter);
        }
        TextRenderer.DrawText(g, "Día del mes", Tema.FuenteEtiqueta,
            new Rectangle(zona.Left, zona.Bottom + 22, zona.Width, 20), Tema.Suave, TextFormatFlags.HorizontalCenter);

        using (var eje = new Pen(Tema.Borde))
        {
            g.DrawLine(eje, zona.Left, zona.Top, zona.Left, zona.Bottom);
            g.DrawLine(eje, zona.Left, zona.Bottom, zona.Right, zona.Bottom);
        }

        // Rayas del límite
        using (var limite = new Pen(Tema.Error, 1.5f) { DashStyle = DashStyle.Dash })
        {
            void Raya(decimal valor, string texto)
            {
                float y = Y(valor);
                g.DrawLine(limite, zona.Left, y, zona.Right, y);
                TextRenderer.DrawText(g, $"{texto} {AguaServicio.Ppm(valor)}", Tema.FuentePequenaNegrita,
                    new Rectangle(zona.Right - 200, (int)y - 20, 196, 18), Tema.Error, TextFormatFlags.Right);
            }
            if (maximo is decimal max) Raya(max, "Máximo");
            if (minimo is decimal min && min > 0) Raya(min, "Mínimo");
        }

        // Un punto por control (si un día hay dos del mismo punto de agua, salen los dos, uno encima de otro),
        // unidos con una línea del color de su punto de agua. Primero todas las líneas y luego todos los puntos,
        // para que ninguna línea tape los puntos de otro punto de agua.
        PointF[] Puntos(Serie serie) => serie.Medidas.Select(m => new PointF(X(m.Fecha), Y(m.Cloro))).ToArray();
        foreach (var serie in series)
            if (Puntos(serie) is { Length: > 1 } linea)
                using (var lapiz = new Pen(serie.Color, 2f))
                    g.DrawLines(lapiz, linea);

        foreach (var serie in series)
        {
            var puntos = Puntos(serie);
            for (int i = 0; i < puntos.Length; i++)
            {
                var medida = serie.Medidas[i];
                // Con todos, algo más grandes para que se distinga el color; los de fuera del límite, rojos
                // con un aro del color del punto de agua para saber de cuál son
                float r = (medida.FueraDeRango ? 6f : 4.5f) + (series.Count > 1 ? 1.5f : 0);
                var circulo = new RectangleF(puntos[i].X - r, puntos[i].Y - r, r * 2, r * 2);
                using var relleno = new SolidBrush(medida.FueraDeRango ? Tema.Error : serie.Color);
                g.FillEllipse(relleno, circulo);
                if (medida.FueraDeRango && series.Count > 1)
                    using (var aro = new Pen(serie.Color, 2.5f))
                        g.DrawEllipse(aro, RectangleF.Inflate(circulo, 2.5f, 2.5f));
                else
                    g.DrawEllipse(Pens.White, circulo);

                dibujadas.Add((puntos[i], $"{serie.Nombre}\n{medida.Fecha.ToString("ddd dd/MM", Componentes.Es)}" +
                    (medida.Hora is TimeSpan h ? " a las " + AguaServicio.Hora(h) : "") + ": " +
                    AguaServicio.Ppm(medida.Cloro) + (medida.FueraDeRango ? "  (fuera del límite)" : "")));
            }
        }

        if (conLeyenda) DibujarLeyenda(g, new Rectangle(zona.Right + 24, zona.Top, AnchoLeyenda - 36, zona.Height));
    }

    private void DibujarLeyenda(Graphics g, Rectangle zona)
    {
        int y = zona.Top;
        foreach (var serie in series)
        {
            if (y + 20 > zona.Bottom) break;
            using (var color = new SolidBrush(serie.Color))
                g.FillEllipse(color, zona.Left, y + 5, 10, 10);
            TextRenderer.DrawText(g, serie.Nombre, Font, new Rectangle(zona.Left + 18, y, zona.Width - 18, 20), Tema.Texto,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            y += 24;
        }
    }

    // El dato del punto más cercano al ratón
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        string? texto = dibujadas
            .Select(d => (d.Texto, Distancia: Math.Pow(d.Punto.X - e.X, 2) + Math.Pow(d.Punto.Y - e.Y, 2)))
            .Where(d => d.Distancia <= 100)
            .OrderBy(d => d.Distancia)
            .Select(d => d.Texto)
            .FirstOrDefault();
        if (texto == textoTooltip) return;
        textoTooltip = texto;
        if (texto == null) tooltip.Hide(this);
        else tooltip.Show(texto, this, e.X + 14, e.Y + 14);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        textoTooltip = null;
        tooltip.Hide(this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) tooltip.Dispose();
        base.Dispose(disposing);
    }
}
