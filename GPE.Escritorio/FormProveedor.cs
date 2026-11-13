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
    private readonly DataGridView gridProductos = new();

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

        pila.Controls.Add(TarjetaDatos(p));
        pila.Controls.Add(TarjetaEvaluacion(p));
        pila.Controls.Add(TarjetaRiesgo(p));
        pila.Controls.Add(TarjetaProductos());

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
        var tarjeta = Componentes.Tarjeta("Productos", subtitulo);
        if (datos.Productos.Count == 0) return tarjeta;

        Componentes.EstiloTabla(gridProductos, altoFila: 44);
        gridProductos.Dock = DockStyle.Top;
        gridProductos.ReadOnly = true;
        gridProductos.MultiSelect = false;
        gridProductos.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        gridProductos.ShowCellToolTips = true;
        gridProductos.DefaultCellStyle.SelectionBackColor = Tema.AcentoSuave;
        gridProductos.DefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        gridProductos.ColumnHeadersDefaultCellStyle.Padding = new Padding(10, 0, 0, 0);
        gridProductos.CellPainting += PintarEtiquetas;

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

        // Primero los activos; los de baja al final
        foreach (var x in datos.Productos.OrderBy(x => x.Baja))
            gridProductos.Rows.Add(CrearFila(x));

        Componentes.AjustarAltoTabla(gridProductos);
        tarjeta.Controls.Add(gridProductos);
        return tarjeta;
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
