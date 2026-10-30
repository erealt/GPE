using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

// Qué puntos se revisan en cada máquina o sección (las casillas grises de la hoja).
public class FormPlantillaRutina : Form
{
    private readonly RutinaServicio servicio = new();
    private PlantillaRutina plantilla = new(new(), new(), new());
    private readonly HashSet<(int PuntoId, int SeccionId)> aplica = new();
    private readonly DataGridView grid = new();
    private bool hayCambios;

    // Como en la rutina: una planta cada vez, para que quepan todas sus máquinas
    private string plantaElegida = Planta.Baja;
    private readonly Dictionary<string, RadioButton> pestanas = new();

    // La tabla se ajusta a la ventana; por debajo de estos tamaños, se encoge también la letra
    private const int AnchoPunto = 220, AltoFilaMinimo = 18, AltoFilaMaximo = 40, AltoFilaComoda = 26;
    private const int AnchoSeccionMinimo = 34, AnchoSeccionMaximo = 110;
    private float escalaActual = 1f;
    private Font? fuenteCabeceraEscalada, fuentePuntoEscalada;

    // Se ha añadido alguna máquina o punto (ya está en la base aunque se cancele la plantilla)
    public bool HayNuevos { get; private set; }

    public FormPlantillaRutina()
    {
        Text = "Plantilla de la rutina de calidad";
        Size = new Size(1300, 800);
        MinimumSize = new Size(900, 500);
        StartPosition = FormStartPosition.CenterParent;
        WindowState = FormWindowState.Maximized;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

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
            Text = "Pulsa una casilla para indicar si ese punto se revisa en esa máquina. " +
                   "Pulsa la cabecera de una máquina o el nombre de un punto para cambiar toda la columna o la fila.\n" +
                   "Lo ya marcado en rondas anteriores no se pierde.",
            AutoSize = true,
            MaximumSize = new Size(1200, 0),
            ForeColor = Tema.Suave,
            Margin = new Padding(0, 0, 0, 8)
        }, 0, 0);
        layout.Controls.Add(CrearBarra(), 0, 1);
        layout.Controls.Add(ConfigurarGrid(), 0, 2);

        var btnGuardar = Tema.BotonPrincipal("Guardar plantilla", 180);
        btnGuardar.Click += (s, e) => Guardar();
        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCancelar;
        var acciones = Componentes.Acciones(btnGuardar, btnCancelar);
        acciones.Margin = new Padding(0, 10, 0, 0);
        layout.Controls.Add(acciones, 0, 3);

        Controls.Add(layout);
        Load += (s, e) => Cargar();
        FormClosing += (s, e) =>
        {
            if (DialogResult != DialogResult.OK && hayCambios
                && !Mensajes.DescartarCambios(this, "Hay cambios sin guardar. ¿Salir sin guardarlos?"))
                e.Cancel = true;
        };
    }

    // Pestañas de planta a la izquierda y botones de añadir a la derecha
    private Control CrearBarra()
    {
        var barra = new Panel { Dock = DockStyle.Top, Height = 46, Margin = new Padding(0, 0, 0, 8) };

        var izquierda = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        foreach (string planta in new[] { Planta.Baja, Planta.Superior })
        {
            var boton = new RadioButton
            {
                Text = Planta.Nombre(planta),
                Appearance = Appearance.Button,
                FlatStyle = FlatStyle.Flat,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(220, 44),
                Margin = new Padding(0, 0, 6, 0),
                Cursor = Cursors.Hand,
                Checked = planta == plantaElegida
            };
            boton.CheckedChanged += (s, e) =>
            {
                ModuloRutinaCalidad.PintarPestana(boton);
                if (!boton.Checked) return;
                plantaElegida = planta;
                Montar();
            };
            ModuloRutinaCalidad.PintarPestana(boton);
            pestanas[planta] = boton;
            izquierda.Controls.Add(boton);
        }

        var btnMaquina = Tema.BotonSecundario("+ Añadir máquina", 180);
        btnMaquina.Click += (s, e) => Anadir(esMaquina: true);
        var btnPunto = Tema.BotonSecundario("+ Añadir punto de control", 230);
        btnPunto.Click += (s, e) => Anadir(esMaquina: false);
        var derecha = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false };
        foreach (var boton in new[] { btnMaquina, btnPunto })
        {
            // Que crezca con el texto: con la letra ampliada de Windows no cabía
            boton.MinimumSize = new Size(boton.Width, 44);
            boton.AutoSize = true;
            boton.Padding = new Padding(10, 0, 10, 0);
            boton.Margin = new Padding(10, 0, 0, 0);
            derecha.Controls.Add(boton);
        }

        barra.Controls.Add(izquierda);
        barra.Controls.Add(derecha);
        return barra;
    }

    private List<RutinaSeccion> SeccionesVisibles =>
        plantilla.Secciones.Where(s => s.Planta == plantaElegida).ToList();

    private Control ConfigurarGrid()
    {
        Componentes.EstiloTabla(grid, altoFila: 30);
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.Single;
        grid.GridColor = Tema.LineaTabla;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
        grid.AllowUserToResizeColumns = false;
        grid.MultiSelect = false;
        grid.ColumnHeadersHeight = 76;
        grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.True;
        grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;

        grid.CellPainting += Pintar;
        grid.CellClick += (s, e) =>
        {
            if (e.RowIndex < 0) return;
            var punto = (RutinaPunto)grid.Rows[e.RowIndex].Tag!;
            var secciones = e.ColumnIndex == 0
                ? SeccionesVisibles
                : new List<RutinaSeccion> { (RutinaSeccion)grid.Columns[e.ColumnIndex].Tag! };
            Cambiar(secciones.Select(sec => (punto.Id, sec.Id)));
        };
        grid.ColumnHeaderMouseClick += (s, e) =>
        {
            if (e.ColumnIndex <= 0) return;
            var seccion = (RutinaSeccion)grid.Columns[e.ColumnIndex].Tag!;
            Cambiar(plantilla.Puntos.Select(p => (p.Id, seccion.Id)));
        };
        grid.Resize += (s, e) => Ajustar();
        return grid;
    }

    // Si todas estaban puestas, las quita; si no, las pone todas
    private void Cambiar(IEnumerable<(int, int)> casillas)
    {
        var lista = casillas.ToList();
        bool quitar = lista.All(aplica.Contains);
        foreach (var casilla in lista)
            if (quitar) aplica.Remove(casilla); else aplica.Add(casilla);
        hayCambios = true;
        grid.Invalidate();
    }

    private void Cargar()
    {
        try
        {
            plantilla = servicio.ObtenerPlantilla();
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo cargar la plantilla.", ex);
            return;
        }
        aplica.UnionWith(plantilla.Aplica);
        Montar();
    }

    // Las máquinas y puntos nuevos se guardan al momento; lo marcado sin guardar se mantiene
    private void Anadir(bool esMaquina)
    {
        string planta;
        using (var ventana = new FormNuevoRutina(esMaquina))
        {
            if (ventana.ShowDialog(this) != DialogResult.OK) return;
            planta = ventana.PlantaElegida;
        }
        HayNuevos = true;

        PlantillaRutina nueva;
        try
        {
            nueva = servicio.ObtenerPlantilla();
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "Se ha añadido, pero no se pudo recargar la plantilla.", ex);
            return;
        }
        plantilla = plantilla with { Secciones = nueva.Secciones, Puntos = nueva.Puntos };

        // Una máquina de la otra planta: se pasa a su pestaña para verla (y eso ya vuelve a montar la tabla)
        if (esMaquina && planta != plantaElegida)
            pestanas[planta].Checked = true;
        else
            Montar();
    }

    private void Montar()
    {
        grid.Rows.Clear();
        grid.Columns.Clear();
        escalaActual = 1f;   // las columnas nuevas vienen con la letra normal

        var columnaPunto = new DataGridViewTextBoxColumn
        {
            HeaderText = "PUNTO DE CONTROL",
            Width = AnchoPunto,
            Frozen = true,
            SortMode = DataGridViewColumnSortMode.NotSortable
        };
        columnaPunto.DefaultCellStyle.Padding = new Padding(10, 0, 4, 0);
        columnaPunto.DefaultCellStyle.BackColor = Tema.CabeceraTabla;
        columnaPunto.DefaultCellStyle.SelectionBackColor = Tema.CabeceraTabla;
        columnaPunto.DefaultCellStyle.Font = Tema.FuentePequenaNegrita;
        grid.Columns.Add(columnaPunto);

        foreach (var seccion in SeccionesVisibles)
        {
            var columna = new DataGridViewTextBoxColumn
            {
                HeaderText = seccion.Nombre,
                Width = ModuloRutinaCalidad.AnchoColumna(seccion.Nombre),
                Tag = seccion,
                SortMode = DataGridViewColumnSortMode.NotSortable
            };
            grid.Columns.Add(columna);
        }

        foreach (var punto in plantilla.Puntos)
        {
            int i = grid.Rows.Add(punto.Nombre);
            grid.Rows[i].Tag = punto;
        }
        grid.ClearSelection();
        PonerEscala(1f, forzar: true);
        Ajustar();
    }

    // Reparte el alto entre las filas y el ancho entre las máquinas, para que se vea todo sin scroll.
    // Mismo criterio que la tabla de la rutina en pantalla completa.
    private void Ajustar()
    {
        if (grid.Columns.Count == 0) return;

        int altoDisponible = grid.ClientSize.Height - grid.ColumnHeadersHeight - 2;
        int alto = grid.Rows.Count == 0 ? AltoFilaMaximo
            : Math.Clamp(altoDisponible / grid.Rows.Count, AltoFilaMinimo, AltoFilaMaximo);
        grid.RowTemplate.Height = alto;
        foreach (DataGridViewRow fila in grid.Rows)
            if (fila.Height != alto) fila.Height = alto;

        var columnas = grid.Columns.Cast<DataGridViewColumn>().Where(c => c.Tag != null).ToList();
        int disponible = grid.ClientSize.Width - 2;
        if (grid.Controls.OfType<VScrollBar>().Any(b => b.Visible))
            disponible -= SystemInformation.VerticalScrollBarWidth;

        // Si ni con lo mínimo cabe, se encoge todo por igual, letra incluida
        var minimos = columnas.Select(c => AnchoMinimo(c.HeaderText)).ToList();
        float escala = Math.Min(1f, (float)disponible / (AnchoPunto + minimos.Sum()));
        PonerEscala(Math.Min(escala, Math.Min(1f, (float)alto / AltoFilaComoda)));
        int anchoPunto = (int)(AnchoPunto * escala);
        grid.Columns[0].Width = anchoPunto;
        if (columnas.Count == 0) return;

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

    // Lo justo para que la palabra más larga del nombre quepa en la cabecera
    private static int AnchoMinimo(string nombre) =>
        Math.Max(AnchoSeccionMinimo, nombre.Split(' ').Max(palabra => TextRenderer.MeasureText(palabra, Tema.FuenteEtiqueta).Width) + 12);

    // Letra de la cabecera y de los puntos a la escala de la tabla (1 = tamaño normal)
    private void PonerEscala(float escala, bool forzar = false)
    {
        escala = (float)Math.Round(escala, 2);
        if (escala == escalaActual && !forzar) return;
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

    private void Pintar(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.Graphics == null || e.RowIndex < 0 || e.ColumnIndex < 1) return;
        var punto = (RutinaPunto)grid.Rows[e.RowIndex].Tag!;
        var seccion = (RutinaSeccion)grid.Columns[e.ColumnIndex].Tag!;
        bool revisa = aplica.Contains((punto.Id, seccion.Id));

        var r = e.CellBounds;
        using (var relleno = new SolidBrush(revisa ? Color.White : Tema.FondoNoRevisa))
            e.Graphics.FillRectangle(relleno, r);
        using (var borde = new Pen(Tema.LineaTabla))
        {
            e.Graphics.DrawLine(borde, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
            e.Graphics.DrawLine(borde, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
        }
        if (revisa)
            TextRenderer.DrawText(e.Graphics, "✓", Tema.FuenteNegrita, r, Tema.Acento,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        e.Handled = true;
    }

    private void Guardar()
    {
        // Las de puntos o secciones desactivados no salen en la tabla: se quedan como estaban
        var casillas = aplica.Concat(plantilla.Aplica.Where(a =>
                !plantilla.Puntos.Any(p => p.Id == a.PuntoId) || !plantilla.Secciones.Any(s => s.Id == a.SeccionId)))
            .ToHashSet();
        try
        {
            servicio.GuardarPlantilla(casillas);
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar la plantilla.", ex);
            return;
        }
        DialogResult = DialogResult.OK;
    }
}
