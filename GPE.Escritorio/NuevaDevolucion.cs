using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

public class FormNuevaDevolucion : Form
{
    private static readonly Color ColorFondo = Tema.Fondo;
    private static readonly Color ColorTexto = Tema.Texto;
    private static readonly Color ColorSuave = Tema.Suave;

    private const int FabricaProvisional = 1;

    private readonly DevolucionServicio servicio = new();

    public int IdCreado { get; private set; }

    // Datos cargados de la base de datos
    private List<Producto> productos = new();
    private bool cargaCorrecta;

    // 1. Datos de la devolución
    private readonly ComboBox cmbCliente = new();
    private readonly DateTimePicker dtpSolicitud = new();
    private readonly ComboBox cmbMotivo = new();
    private readonly TextBox txtAlbaran = new();
    private readonly TextBox txtObservaciones = new();

    private record FilaProducto(
        TableLayoutPanel Panel,
        ComboBox Producto,
        NumericUpDown Cantidad,
        TextBox Lote);

    // Mensajes de error que aparecen debajo de cada campo
    private readonly Dictionary<Control, Label> errores = new();
    private readonly Label lblErrorProductos = CrearEtiquetaError();

    private readonly Label lblResumenProductos = new();
    private readonly TableLayoutPanel contenedorFilas = new();
    private readonly List<FilaProducto> filas = new();

    public FormNuevaDevolucion()
    {
        Text = "Nueva devolución";
        Size = new Size(1200, 750);
        MinimumSize = new Size(1150, 600);
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

        try
        {
            var clientes = servicio.ObtenerClientes();
            productos = servicio.ObtenerProductos();

            cmbCliente.Items.AddRange(clientes.Cast<object>().ToArray());
            cargaCorrecta = true;
        }
        catch (DbException ex)   
        {
            MessageBox.Show(this,
                "No se pudieron cargar los clientes y productos de la base de datos.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        cmbMotivo.Items.AddRange(new object[]
            { "Producto dañado", "Error en pedido", "Caducidad próxima", "Envase defectuoso", "Otro" });

        dtpSolicitud.Format = DateTimePickerFormat.Short;
        dtpSolicitud.MaxDate = DateTime.Today;   // no se admiten solicitudes futuras

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
        var btnAnadir = Tema.BotonSecundario("+  Añadir producto", 190);
        btnAnadir.Click += (s, e) => AnadirFilaProducto();

        var tarjeta = CrearTarjeta("2", "Productos esperados", "", btnAnadir, lblResumenProductos);

        contenedorFilas.Dock = DockStyle.Fill;
        contenedorFilas.AutoSize = true;
        contenedorFilas.ColumnCount = 1;
        contenedorFilas.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        lblErrorProductos.Margin = new Padding(0, 10, 0, 0);

        tarjeta.Controls.Add(CrearEncabezadoProductos());
        tarjeta.Controls.Add(contenedorFilas);
        tarjeta.Controls.Add(lblErrorProductos);

        AnadirFilaProducto();   
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

        var btnGuardar = Tema.BotonPrincipal("Guardar devolución", 190);
        btnGuardar.Height = 45;
        btnGuardar.Margin = new Padding(0);
        btnGuardar.Enabled = cargaCorrecta;
        btnGuardar.Click += (s, e) => Guardar();

        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.Height = 45;
        btnCancelar.Margin = new Padding(0, 0, 10, 0);
        btnCancelar.DialogResult = DialogResult.Cancel;

        CancelButton = btnCancelar;   

        panel.Controls.Add(btnGuardar);
        panel.Controls.Add(btnCancelar);

        return panel;
    }

    // ---------- Guardado ----------

    private void Guardar()
    {
        // 1. Validaciones: se marcan todos los campos con error a la vez
        LimpiarErrores();
        Control? primerError = null;

        if (cmbCliente.SelectedItem is not Cliente)
        {
            MarcarError(cmbCliente, "Selecciona un cliente.");
            primerError ??= cmbCliente;
        }

        if (cmbMotivo.SelectedItem is not string)
        {
            MarcarError(cmbMotivo, "Selecciona el motivo de la devolución.");
            primerError ??= cmbMotivo;
        }

        var filasSinProducto = filas.Where(f => f.Producto.SelectedItem is not Producto).ToList();
        if (filasSinProducto.Count == filas.Count)
        {
            MostrarErrorProductos("Añade al menos un producto a la devolución.");
            primerError ??= filas[0].Producto;
        }
        else if (filasSinProducto.Count > 0)
        {
            MostrarErrorProductos("Hay filas sin producto: elige uno o quítalas con ✕.");
            primerError ??= filasSinProducto[0].Producto;
        }

        if (primerError != null)
        {
            primerError.Focus();
            return;
        }

        var cliente = (Cliente)cmbCliente.SelectedItem!;
        var motivo = (string)cmbMotivo.SelectedItem!;

        var lineas = filas
            .Where(f => f.Producto.SelectedItem is Producto)
            .Select(f => new LineaNuevaDevolucion(
                ((Producto)f.Producto.SelectedItem!).Id,
                (int)f.Cantidad.Value,
                string.IsNullOrWhiteSpace(f.Lote.Text) ? null : f.Lote.Text.Trim()))
            .ToList();

        // 2. Guardar en la base de datos
        var datos = new DatosNuevaDevolucion(
            FabricaProvisional,
            cliente.Id,
            motivo,
            string.IsNullOrWhiteSpace(txtAlbaran.Text) ? null : txtAlbaran.Text.Trim(),
            string.IsNullOrWhiteSpace(txtObservaciones.Text) ? null : txtObservaciones.Text.Trim(),
            dtpSolicitud.Value,
            lineas);

        try
        {
            IdCreado = servicio.CrearDevolucion(datos);
        }
        catch (ReglaNegocioException ex)   
        {
            MessageBox.Show(this, ex.Message, "Faltan datos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        catch (DbException ex)
        {
            MessageBox.Show(this,
                "No se pudo guardar la devolución.\n\n" + ex.Message,
                "Error de base de datos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        DialogResult = DialogResult.OK;
    }

    // ---------- Mensajes de error ----------

    private static Label CrearEtiquetaError() => new()
    {
        AutoSize = true,
        ForeColor = Tema.Error,
        Font = new Font("Segoe UI", 9),
        Margin = new Padding(0, 4, 0, 0),
        Visible = false
    };

    private void MarcarError(Control campo, string mensaje)
    {
        var etiqueta = errores[campo];
        etiqueta.Text = mensaje;
        etiqueta.Visible = true;
    }

    private void MostrarErrorProductos(string mensaje)
    {
        lblErrorProductos.Text = mensaje;
        lblErrorProductos.Visible = true;
    }

    private void LimpiarErrores()
    {
        foreach (var etiqueta in errores.Values)
            etiqueta.Visible = false;
        lblErrorProductos.Visible = false;
    }

    // ---------- Filas de producto ----------

    private static TableLayoutPanel PlantillaFilaProducto()
    {
        var fila = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5 };
        foreach (float peso in new float[] { 36, 18, 18, 28 })
            fila.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, peso));
        fila.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 45));
        return fila;
    }

    private Control CrearEncabezadoProductos()
    {
        var encabezado = PlantillaFilaProducto();
        encabezado.BackColor = Tema.CabeceraTabla;

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
        cmbProducto.Items.AddRange(productos.Cast<object>().ToArray());

        var txtReferencia = new TextBox { ReadOnly = true, PlaceholderText = "REF-0000" };
        // Mínimo 1: la base de datos no admite cantidades de 0
        var numCantidad = new NumericUpDown { Minimum = 1, Maximum = 100000 };
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

        cmbProducto.SelectedIndexChanged += (s, e) =>
        {
            txtReferencia.Text = cmbProducto.SelectedItem is Producto p ? p.Codigo : "";
            lblErrorProductos.Visible = false;
            ActualizarResumen();
        };
        numCantidad.ValueChanged += (s, e) => ActualizarResumen();

        btnQuitar.Click += (s, e) =>
        {
            if (filas.Count <= 1) return;  
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
        textos.Controls.Add(lblTitulo);   

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

    private Control Campo(string etiqueta, Control control)
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

        // Al corregir el campo desaparece su error
        var error = CrearEtiquetaError();
        errores[control] = error;
        panel.Controls.Add(error);
        if (control is ComboBox combo)
            combo.SelectedIndexChanged += (s, e) => error.Visible = false;

        return panel;
    }
}