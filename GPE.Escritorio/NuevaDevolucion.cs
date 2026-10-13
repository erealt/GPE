namespace GPE.Escritorio;

public class FormNuevaDevolucion : Form
{
    private static readonly Color ColorFondo = Color.FromArgb(245, 246, 244);
    private static readonly Color ColorTexto = Color.FromArgb(20, 35, 30);
    private static readonly Color ColorSuave = Color.FromArgb(110, 120, 115);

    // 1. Datos de la devolución
    private readonly ComboBox cmbCliente = new();
    private readonly DateTimePicker dtpSolicitud = new();
    private readonly ComboBox cmbMotivo = new();
    private readonly TextBox txtAlbaran = new();
    private readonly TextBox txtObservaciones = new();

    // Productos esperados (cada fila es un conjunto de controles)
    private record FilaProducto(
        TableLayoutPanel Panel,
        ComboBox Producto,
        NumericUpDown Cantidad,
        TextBox Lote);

    private readonly Label lblResumenProductos = new();   // "2 productos · 36 unidades"
    private readonly TableLayoutPanel contenedorFilas = new();
    private readonly List<FilaProducto> filas = new();

    // Datos inventados: más adelante vendrán de la base de datos
    private readonly Dictionary<string, string> catalogoProductos = new()
    {
        { "Bombones surtidos 250 g", "REF-0001" },
        { "Tableta chocolate negro 100 g", "REF-0002" },
        { "Trufas de cacao 200 g", "REF-0003" },
        { "Praliné de avellana 150 g", "REF-0004" }
    };

    public FormNuevaDevolucion()
    {
        Text = "Nueva devolución";
        Size = new Size(1000, 800);
        MinimumSize = new Size(800, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = ColorFondo;
        Font = new Font("Segoe UI", 10);

        PrepararControles();
        
        Controls.Add(CrearContenido());
    }

    // ---------- Preparación de controles ----------

    private void PrepararControles()
    {
        cmbCliente.DropDownStyle = ComboBoxStyle.DropDownList;
        cmbMotivo.DropDownStyle = ComboBoxStyle.DropDownList;

        cmbCliente.Items.AddRange(new object[]
            { "Distribuciones Rivera", "Alimentación Sánchez", "Supermercados Norte", "Comercial del Sur" });
        cmbMotivo.Items.AddRange(new object[]
            { "Producto dañado", "Error en pedido", "Caducidad próxima", "Envase defectuoso", "Otro" });

        dtpSolicitud.Format = DateTimePickerFormat.Short;   // por defecto: hoy

        txtAlbaran.PlaceholderText = "Ej. ALB-45872";
        txtObservaciones.PlaceholderText = "Añade cualquier información relevante sobre la solicitud...";
        txtObservaciones.Multiline = true;
        txtObservaciones.Height = 80;
        txtObservaciones.ScrollBars = ScrollBars.Vertical;
    }

    // ---------- Estructura general ----------

    private Panel CrearContenido()
    {
        var scroll = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(40, 25, 40, 25)
        };

        // Pila vertical: cada hijo es una fila que ocupa todo el ancho
        var pila = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1
        };
        pila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        pila.Controls.Add(CrearCabecera());
        pila.Controls.Add(CrearTarjetaDatos());
        pila.Controls.Add(CrearTarjetaProductos());
        pila.Controls.Add(CrearAcciones());

        scroll.Controls.Add(pila);
        return scroll;
    }

    private Control CrearCabecera()
    {
        var panel = new Panel { Dock = DockStyle.Fill, Height = 120, Margin = new Padding(0, 0, 0, 10) };

        var lblSubtitulo = new Label
        {
            Text = "Completa los datos para iniciar el proceso de devolución.",
            Dock = DockStyle.Top,
            Height = 28,
            ForeColor = ColorSuave
        };
        var lblTitulo = new Label
        {
            Text = "Nueva devolución",
            Dock = DockStyle.Top,
            Height = 50,
            Font = new Font("Segoe UI", 22, FontStyle.Bold),
            ForeColor = ColorTexto
        };
        var lblRuta = new Label
        {
            Text = "DEVOLUCIONES / NUEVA",
            Dock = DockStyle.Top,
            Height = 25,
            ForeColor = ColorSuave,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };

        // El último añadido queda arriba
        panel.Controls.Add(lblSubtitulo);
        panel.Controls.Add(lblTitulo);
        panel.Controls.Add(lblRuta);
        return panel;
    }

    // ---------- Tarjeta 1: datos ----------

    private Control CrearTarjetaDatos()
    {
        var tarjeta = CrearTarjeta("1", "Datos de la devolución", "Información general de la solicitud del cliente");

        var rejilla = Rejilla(2);
        rejilla.Controls.Add(Campo("Cliente", cmbCliente));
        rejilla.Controls.Add(Campo("Fecha de solicitud", dtpSolicitud));
        rejilla.Controls.Add(Campo("Motivo de devolución", cmbMotivo));
        rejilla.Controls.Add(Campo("Albarán relacionado", txtAlbaran));

        var campoObservaciones = Campo("Observaciones", txtObservaciones);
        rejilla.Controls.Add(campoObservaciones);
        rejilla.SetColumnSpan(campoObservaciones, 2);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // ---------- Tarjeta: productos esperados ----------

    private Control CrearTarjetaProductos()
    {
        var btnAnadir = CrearBotonSecundario("+  Añadir producto", 170);
        btnAnadir.Click += (s, e) => AnadirFilaProducto();

        // El subtítulo es el resumen ("N productos · M unidades") y se actualiza solo
        var tarjeta = CrearTarjeta("", "Productos esperados", "", btnAnadir, lblResumenProductos);

        contenedorFilas.Dock = DockStyle.Fill;
        contenedorFilas.AutoSize = true;
        contenedorFilas.ColumnCount = 1;
        contenedorFilas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        tarjeta.Controls.Add(CrearEncabezadoProductos());
        tarjeta.Controls.Add(contenedorFilas);

        AnadirFilaProducto();   // una fila vacía de partida
        return tarjeta;
    }
    
private Control CrearAcciones()
{
    var panel = new FlowLayoutPanel
    {
        Dock = DockStyle.Top,
        AutoSize = true,
        FlowDirection = FlowDirection.RightToLeft,
        WrapContents = false,
        Margin = new Padding(0, 0, 0, 20)
    };

    var btnCrear = new Button
    {
        Text = "Crear recogida →",
        Size = new Size(190, 45),
        FlatStyle = FlatStyle.Flat,
        BackColor = Color.FromArgb(35, 90, 65),
        ForeColor = Color.White,
        Font = new Font("Segoe UI", 10, FontStyle.Bold),
        Cursor = Cursors.Hand,
        Margin = new Padding(0)
    };

    btnCrear.FlatAppearance.BorderSize = 0;

    btnCrear.Click += (s, e) =>
    { var pantalla = new Recogida();

    pantalla.Show();

    this.Hide();
    };

    panel.Controls.Add(btnCrear);

    return panel;
}


    private static TableLayoutPanel PlantillaFilaProducto()
    {
        // Columnas: producto, referencia, cantidad esperada, lote, botón quitar
        var fila = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5 };
        foreach (float peso in new float[] { 36, 18, 18, 28 })
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, peso));
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
        return fila;
    }

    private Control CrearEncabezadoProductos()
    {
        var encabezado = PlantillaFilaProducto();
        encabezado.BackColor = Color.FromArgb(243, 244, 241);

        string[] titulos = { "PRODUCTO", "REFERENCIA", "CANTIDAD ESPERADA", "LOTE", "" };
        for (int i = 0; i < titulos.Length; i++)
        {
            encabezado.Controls.Add(new Label
            {
                Text = titulos[i],
                Dock = DockStyle.Fill,
                Height = 40,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = ColorSuave,
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
            }, i, 0);
        }
        return encabezado;
    }

    private void AnadirFilaProducto()
    {
        var panel = PlantillaFilaProducto();
        panel.Margin = new Padding(0, 8, 0, 0);

        var cmbProducto = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        cmbProducto.Items.AddRange(catalogoProductos.Keys.Cast<object>().ToArray());

        var txtReferencia = new TextBox { ReadOnly = true, PlaceholderText = "REF-0000" };
        var numCantidad = new NumericUpDown { Minimum = 0, Maximum = 100000 };
        var txtLote = new TextBox { PlaceholderText = "Lote" };

        var btnQuitar = new Button
        {
            Text = "✕",
            FlatStyle = FlatStyle.Flat,
            ForeColor = Color.Firebrick,
            Cursor = Cursors.Hand
        };
        btnQuitar.FlatAppearance.BorderSize = 0;

        var registro = new FilaProducto(panel, cmbProducto, numCantidad, txtLote);

        // Al elegir un producto se rellena su referencia y se recalcula el resumen
        cmbProducto.SelectedIndexChanged += (s, e) =>
        {
            txtReferencia.Text = catalogoProductos[cmbProducto.Text];
            ActualizarResumen();
        };
        numCantidad.ValueChanged += (s, e) => ActualizarResumen();

        btnQuitar.Click += (s, e) =>
        {
            if (filas.Count <= 1) return;   // siempre dejamos al menos una fila
            filas.Remove(registro);
            contenedorFilas.Controls.Remove(panel);
            panel.Dispose();
            ActualizarResumen();
        };

        Control[] controles = { cmbProducto, txtReferencia, numCantidad, txtLote, btnQuitar };
        for (int i = 0; i < controles.Length; i++)
        {
            controles[i].Dock = DockStyle.Fill;
            controles[i].Margin = new Padding(0, 0, 8, 0);
            panel.Controls.Add(controles[i], i, 0);
        }

        filas.Add(registro);
        contenedorFilas.Controls.Add(panel);
        ActualizarResumen();
    }

    private void ActualizarResumen()
    {
        var conProducto = filas.Where(f => f.Producto.SelectedIndex >= 0).ToList();
        int unidades = (int)conProducto.Sum(f => f.Cantidad.Value);

        lblResumenProductos.Text = conProducto.Count == 0
            ? "Aún no hay productos"
            : $"{conProducto.Count} {(conProducto.Count == 1 ? "producto" : "productos")} · {unidades} unidades";
    }

    // ---------- Ayudas para construir la interfaz ----------

    private TableLayoutPanel CrearTarjeta(string numero, string titulo, string subtitulo,
        Control? accion = null, Label? etiquetaSubtitulo = null)
    {
        var tarjeta = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.White,
            Padding = new Padding(25),
            Margin = new Padding(0, 0, 0, 20)
        };
        tarjeta.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        // Fila de cabecera: textos a la izquierda, botón opcional a la derecha
        var cabecera = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 2,
            Margin = new Padding(0, 0, 0, 15)
        };
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        cabecera.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var textos = new Panel { Dock = DockStyle.Fill, Height = 55 };

        var lblSubtitulo = etiquetaSubtitulo ?? new Label();
        lblSubtitulo.Text = subtitulo;
        lblSubtitulo.Dock = DockStyle.Top;
        lblSubtitulo.Height = 22;
        lblSubtitulo.ForeColor = ColorSuave;
        lblSubtitulo.Font = new Font("Segoe UI", 9);

        var lblTitulo = new Label
        {
            Text = numero == "" ? titulo : $"{numero}   {titulo}",
            Dock = DockStyle.Top,
            Height = 30,
            Font = new Font("Segoe UI", 13, numero == "" ? FontStyle.Bold : FontStyle.Regular),
            ForeColor = ColorTexto
        };
        textos.Controls.Add(lblSubtitulo);
        textos.Controls.Add(lblTitulo);   // el último añadido queda arriba

        cabecera.Controls.Add(textos, 0, 0);
        if (accion != null) cabecera.Controls.Add(accion, 1, 0);

        tarjeta.Controls.Add(cabecera);
        return tarjeta;
    }

    private static TableLayoutPanel Rejilla(int columnas)
    {
        var rejilla = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = columnas };
        for (int i = 0; i < columnas; i++)
            rejilla.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / columnas));
        return rejilla;
    }

    private static Control Campo(string etiqueta, Control control)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            ColumnCount = 1,
            Margin = new Padding(0, 0, 15, 15)
        };
        panel.Controls.Add(new Label
        {
            Text = etiqueta,
            AutoSize = true,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 4)
        });

        control.Dock = DockStyle.Top;
        panel.Controls.Add(control);
        return panel;
    }

    private static Button CrearBotonSecundario(string texto, int ancho)
    {
        var boton = new Button
        {
            Text = texto,
            Size = new Size(ancho, 46),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = ColorTexto,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        boton.FlatAppearance.BorderColor = Color.FromArgb(200, 205, 200);
        return boton;
    }
}