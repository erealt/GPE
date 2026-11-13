using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Ficha de un proveedor (solo consulta): sus datos de la hoja PRODUCTOS (columnas A-N y riesgo del proveedor)
// y, debajo, la tabla con todos sus productos.
public class FormProveedor : Form
{
    private const int ColumnaEstado = 3;
    private const int ColumnaMuestreo = 4;
    private const int ColumnaRiesgo = 5;

    private readonly ProveedorConProductos datos;
    private const string EstadoActivos = "Activos";
    private const string EstadoBaja = "De baja";
    private const string EstadoTodos = "Todos";

    private readonly DataGridView gridProductos = new();
    private readonly ComboBox cmbEstadoProductos = new();

    public FormProveedor(ProveedorConProductos proveedor)
    {
        datos = proveedor;
        var p = proveedor.Proveedor;

        Text = $"Proveedor {p.Numero} - {p.Nombre}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1000, 650);
        Componentes.AjustarAPantalla(this, 0.85);
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var (scroll, pila) = Componentes.Pagina();

        string subtitulo = string.Join("  ·  ", new[] { p.Tipo, p.Baja ? "De baja" : "Activo" }.OfType<string>());
        pila.Controls.Add(Componentes.Cabecera($"PROVEEDORES  ·  Nº {p.Numero?.ToString() ?? "—"}", p.Nombre, subtitulo));

        if (p.Revisar != null)
            pila.Controls.Add(AvisoDatosDistintos(p.Revisar));

        pila.Controls.Add(FranjaResumen());
        pila.Controls.Add(Marcada(TarjetaDatos(p), EstiloProveedores.RayaDatos));
        pila.Controls.Add(Marcada(TarjetaEvaluacion(p), EstiloProveedores.RayaEvaluacion));
        pila.Controls.Add(Marcada(TarjetaRiesgo(p), EstiloProveedores.RayaRiesgo));
        pila.Controls.Add(Marcada(TarjetaProductos(), EstiloProveedores.RayaProductos));

        var btnCerrar = Tema.BotonSecundario("Cerrar", 140);
        btnCerrar.Click += (s, e) => Close();
        pila.Controls.Add(Componentes.Acciones(btnCerrar));

        Controls.Add(scroll);
        // El botón Cerrar coge el foco y la página bajaría hasta él: se vuelve arriba
        Shown += (s, e) =>
        {
            gridProductos.ClearSelection();
            scroll.AutoScrollPosition = Point.Empty;
        };
    }

    private static Control Marcada(Control tarjeta, Color color)
    {
        EstiloProveedores.MarcarTarjeta(tarjeta, color);
        return tarjeta;
    }

    // Cuatro recuadros arriba con lo más importante, para verlo sin leer las tarjetas
    private Control FranjaResumen()
    {
        var p = datos.Proveedor;
        string? evaluacion = ProveedorServicio.Evaluacion(p);
        string? riesgo = ProveedorServicio.RiesgoFraudeMasAlto(datos);
        int activos = datos.Productos.Count(x => !x.Baja);
        int deBaja = datos.Productos.Count - activos;

        var franja = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 4,
            RowCount = 1,
            Margin = new Padding(0, 0, 0, 20)
        };
        for (int i = 0; i < 4; i++)
            franja.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
        franja.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var (fondoEval, colorEval) = Tema.ColorNotaProveedor(evaluacion ?? "");
        var (fondoRiesgo, colorRiesgo) = Tema.ColorNotaProveedor(riesgo ?? "");
        franja.Controls.Add(Recuadro("Evaluación", evaluacion ?? "—",
            evaluacion == null ? "Faltan datos para calcularla" : $"Impacto + estatus + reclamaciones = {(p.Impacto ?? 0) + (p.Estatus ?? 0) + (p.Reclamaciones ?? 0)}",
            fondoEval, colorEval, ultimo: false), 0, 0);
        franja.Controls.Add(Recuadro("Riesgo de fraude más alto", riesgo ?? "—",
            "De sus productos activos", fondoRiesgo, colorRiesgo, ultimo: false), 1, 0);
        franja.Controls.Add(Recuadro("Productos activos", activos.ToString(),
            deBaja > 0 ? $"y {deBaja} de baja" : "Ninguno de baja", Color.White, Tema.Texto, ultimo: false), 2, 0);
        franja.Controls.Add(Recuadro("Reclamaciones", Nota(p.Reclamaciones),
            "Nota de 0 a 2", Color.White, Tema.Texto, ultimo: true), 3, 0);
        return franja;
    }

    private static Control Recuadro(string etiqueta, string valor, string nota, Color fondo, Color color, bool ultimo)
    {
        var recuadro = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            BackColor = fondo,
            Padding = new Padding(20, 15, 20, 15),
            Margin = new Padding(0, 0, ultimo ? 0 : 15, 0)
        };
        recuadro.Controls.Add(new Label
        {
            Text = etiqueta.ToUpper(Componentes.Es),
            AutoSize = true,
            ForeColor = fondo == Color.White ? Tema.Suave : color,
            Font = Tema.FuenteEtiqueta
        });
        recuadro.Controls.Add(new Label
        {
            Text = valor,
            AutoSize = true,
            ForeColor = color,
            Font = EstiloProveedores.FuenteCifra,
            Margin = new Padding(0, 6, 0, 2)
        });
        recuadro.Controls.Add(new Label
        {
            Text = nota,
            AutoSize = true,
            ForeColor = fondo == Color.White ? Tema.Suave : color,
            Font = Tema.FuenteNota
        });
        return recuadro;
    }

    // Datos que en el Excel eran distintos según la fila, y cuál se guardó
    private static Control AvisoDatosDistintos(string revisar)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            BackColor = Tema.AvisoFondo,
            Padding = new Padding(20, 15, 20, 15),
            Margin = new Padding(0, 0, 0, 20)
        };
        panel.Controls.Add(new Label
        {
            Text = "⚠  En el Excel este proveedor tenía datos distintos según la fila",
            AutoSize = true,
            Font = Tema.FuenteNegrita,
            ForeColor = Tema.Aviso,
            Margin = new Padding(0, 0, 0, 6)
        });
        panel.Controls.Add(new Label
        {
            Text = revisar,
            AutoSize = true,
            ForeColor = Tema.Texto,
            MaximumSize = new Size(1100, 0)
        });
        return panel;
    }

    private static Control TarjetaDatos(Proveedor p)
    {
        var tarjeta = Componentes.Tarjeta("Datos del proveedor");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(Dato("Nº", p.Numero?.ToString()));
        rejilla.Controls.Add(Dato("Fecha alta", Fecha(p.FechaAlta)));
        rejilla.Controls.Add(Dato("Estado", p.Baja ? "Baja" : "Activo"));
        rejilla.Controls.Add(Dato("Tipo", p.Tipo));
        rejilla.Controls.Add(Dato("Fecha revisión", Fecha(p.FechaRevision)));
        rejilla.Controls.Add(Dato("Certificados", p.Certificados));
        rejilla.Controls.Add(Dato("Ensayo trazabilidad", p.EnsayoTrazabilidad));
        rejilla.Controls.Add(Dato("Auditoría", p.Auditoria));
        rejilla.Controls.Add(Dato("RFA", p.Rfa));
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Evaluación de frecuencia de revisión del proveedor
    private static Control TarjetaEvaluacion(Proveedor p)
    {
        string? evaluacion = ProveedorServicio.Evaluacion(p);
        var tarjeta = Componentes.Tarjeta("Evaluación del proveedor",
            "Impacto + estatus + reclamaciones: de 1 a 3 es A, de 4 a 6 es B y de 7 a 9 es C.");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(Dato("Impacto", Nota(p.Impacto)));
        rejilla.Controls.Add(Dato("Estatus", Nota(p.Estatus)));
        rejilla.Controls.Add(Dato("Reclamaciones", Nota(p.Reclamaciones)));
        rejilla.Controls.Add(DatoEtiqueta("Evaluación", evaluacion,
            evaluacion == null ? "Falta el impacto, el estatus o las reclamaciones" : ProveedorServicio.QueSupone(evaluacion)));
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Riesgo del proveedor para cometer fraude: multiplica el riesgo de cada producto
    private static Control TarjetaRiesgo(Proveedor p)
    {
        var tarjeta = Componentes.Tarjeta("Riesgo del proveedor para cometer fraude",
            "El valor más alto de los tres multiplica el O × D de cada producto para sacar su riesgo de fraude.");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(Dato("Relación", Nota(ProveedorServicio.Relacion(p)),
            p.Relacion == null ? "Igual que el estatus" : "Propia (en el Excel no era el estatus)"));
        rejilla.Controls.Add(Dato("Historial", Nota(p.Historial)));
        rejilla.Controls.Add(Dato("P. subcontratado", Nota(p.Subcontratado)));
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control TarjetaProductos()
    {
        int activos = datos.Productos.Count(x => !x.Baja);
        int deBaja = datos.Productos.Count - activos;
        string subtitulo = datos.Productos.Count == 0
            ? "Este proveedor no tiene productos."
            : $"{activos} {(activos == 1 ? "activo" : "activos")}" + (deBaja > 0 ? $" y {deBaja} de baja" : "")
              + ". El muestreo y el riesgo de fraude son de cada producto.";
        if (datos.Productos.Count == 0) return Componentes.Tarjeta("Productos", subtitulo);

        // Como el filtro del Excel: al abrir se ven solo los productos activos
        cmbEstadoProductos.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbEstadoProductos.Width = 140;
        cmbEstadoProductos.Items.AddRange(new object[] { EstadoActivos, EstadoBaja, EstadoTodos });
        cmbEstadoProductos.SelectedItem = activos > 0 ? EstadoActivos : EstadoTodos;
        cmbEstadoProductos.SelectedIndexChanged += (s, e) => MostrarProductos();
        var tarjeta = Componentes.Tarjeta("Productos", subtitulo, cmbEstadoProductos);

        Componentes.EstiloTabla(gridProductos, altoFila: 44);
        gridProductos.Dock = DockStyle.Top;
        gridProductos.ReadOnly = true;
        gridProductos.MultiSelect = false;
        gridProductos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        gridProductos.ShowCellToolTips = true;
        gridProductos.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        gridProductos.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        gridProductos.CellPainting += PintarEtiquetas;
        EstiloProveedores.TablaMarcada(gridProductos);

        (string Nombre, int Ancho)[] columnas =
        {
            ("CÓDIGO", 130), ("DESCRIPCIÓN", 320), ("TIPO PRODUCTO", 160), ("ESTADO", 100),
            ("MUESTREO", 110), ("RIESGO FRAUDE", 140), ("ACTUACIÓN", 230)
        };
        foreach (var (nombre, ancho) in columnas)
        {
            int i = gridProductos.Columns.Add(nombre, nombre);
            gridProductos.Columns[i].MinimumWidth = ancho;
            gridProductos.Columns[i].FillWeight = ancho;
            gridProductos.Columns[i].SortMode = DataGridViewColumnSortMode.NotSortable;
        }
        gridProductos.Columns[1].FillWeight = 600;

        MostrarProductos();
        tarjeta.Controls.Add(gridProductos);
        return tarjeta;
    }

    private void MostrarProductos()
    {
        string estado = cmbEstadoProductos.SelectedItem as string ?? EstadoTodos;
        gridProductos.Rows.Clear();
        // Con "Todos", primero los activos y los de baja al final
        foreach (var x in datos.Productos.OrderBy(x => x.Baja))
            if (estado == EstadoTodos || x.Baja == (estado == EstadoBaja))
                gridProductos.Rows.Add(CrearFila(x));

        Componentes.AjustarAltoTabla(gridProductos);
        gridProductos.ClearSelection();
    }

    private DataGridViewRow CrearFila(ProductoProveedor x)
    {
        var p = datos.Proveedor;
        string? muestreo = ProveedorServicio.FrecuenciaMuestreo(p, x);
        string? riesgo = ProveedorServicio.RiesgoFraude(p, x);

        // Las filas creadas a mano no cogen el alto de RowTemplate, y AjustarAltoTabla cuenta con él
        var fila = new DataGridViewRow { Tag = x, Height = gridProductos.RowTemplate.Height };
        fila.CreateCells(gridProductos,
            x.Codigo ?? "—",
            x.Descripcion ?? "—",
            x.TipoProducto ?? "—",
            x.Baja ? "Baja" : "Activo",
            muestreo ?? "—",
            riesgo ?? "—",
            ProveedorServicio.Actuacion(riesgo) ?? "—");

        if (x.Baja)
            fila.DefaultCellStyle.ForeColor = Tema.Suave;

        fila.Cells[ColumnaMuestreo].ToolTipText = muestreo == null
            ? "Falta el impacto, el estatus o el control en recepción"
            : $"Impacto {Nota(ProveedorServicio.ImpactoProducto(p, x))} + estatus {Nota(p.Estatus)}"
              + $" + control recepción {Nota(x.ControlRecepcion)} + reclamaciones {Nota(x.Reclamaciones)}"
              + $"\n{ProveedorServicio.AlcanceMuestreo(muestreo)}";
        fila.Cells[ColumnaRiesgo].ToolTipText = riesgo == null
            ? "Faltan las notas de fraude del producto"
            : $"O × D = {ProveedorServicio.OcurrenciaPorDeteccion(x)} (vulnerabilidad {ProveedorServicio.RiesgoVulnerabilidad(x)})"
              + $"\nPuntuación = {ProveedorServicio.Puntuacion(p, x)}";
        return fila;
    }

    // Estado, muestreo y riesgo se dibujan como etiquetas de color
    private void PintarEtiquetas(object? sender, DataGridViewCellPaintingEventArgs e)
    {
        if (e.RowIndex < 0 || e.Graphics == null) return;
        if (gridProductos.Rows[e.RowIndex].Tag is not ProductoProveedor x) return;

        string? texto = e.ColumnIndex switch
        {
            ColumnaEstado => x.Baja ? "Baja" : "Activo",
            ColumnaMuestreo => ProveedorServicio.FrecuenciaMuestreo(datos.Proveedor, x),
            ColumnaRiesgo => ProveedorServicio.RiesgoFraude(datos.Proveedor, x),
            _ => null
        };
        if (texto == null) return;

        // Estado: verde si está activo y gris si es de baja
        var (fondo, color) = e.ColumnIndex == ColumnaEstado
            ? Tema.ColorNotaProveedor(x.Baja ? "" : "A")
            : Tema.ColorNotaProveedor(texto);
        Componentes.PintarEtiqueta(e, texto, fondo, color);
    }

    // Piezas de la ficha

    private static string Fecha(DateTime? fecha) => fecha?.ToString("dd/MM/yyyy") ?? "—";
    private static string Nota(byte? nota) => nota?.ToString() ?? "—";

    // ETIQUETA arriba y valor debajo; con nota, una línea gris más
    private static Control Dato(string etiqueta, string? valor, string? nota = null)
    {
        var dato = PanelDato(etiqueta);
        dato.Controls.Add(new Label
        {
            Text = string.IsNullOrWhiteSpace(valor) ? "—" : valor,
            AutoSize = true,
            ForeColor = Tema.Texto,
            Font = Tema.FuenteNegrita,
            MaximumSize = new Size(280, 0),
            Margin = new Padding(0, 4, 0, 0)
        });
        if (nota != null) dato.Controls.Add(Nota(nota));
        return dato;
    }

    // Como Dato, pero el valor es una etiqueta de color (A/B/C)
    private static Control DatoEtiqueta(string etiqueta, string? valor, string? nota)
    {
        var dato = PanelDato(etiqueta);
        var (fondo, color) = Tema.ColorNotaProveedor(valor ?? "");
        dato.Controls.Add(new Label
        {
            Text = valor ?? "—",
            AutoSize = false,
            Size = new Size(46, 28),
            TextAlign = ContentAlignment.MiddleCenter,
            BackColor = fondo,
            ForeColor = color,
            Font = Tema.FuenteNegrita,
            Margin = new Padding(0, 4, 0, 0)
        });
        if (nota != null) dato.Controls.Add(Nota(nota));
        return dato;
    }

    private static FlowLayoutPanel PanelDato(string etiqueta)
    {
        var dato = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(0, 0, 15, 18)
        };
        dato.Controls.Add(new Label
        {
            Text = etiqueta.ToUpper(Componentes.Es),
            AutoSize = true,
            ForeColor = Tema.Suave,
            Font = Tema.FuenteEtiqueta
        });
        return dato;
    }

    private static Label Nota(string texto) => new()
    {
        Text = texto,
        AutoSize = true,
        ForeColor = Tema.Suave,
        Font = Tema.FuenteNota,
        MaximumSize = new Size(280, 0),
        Margin = new Padding(0, 4, 0, 0)
    };
}
