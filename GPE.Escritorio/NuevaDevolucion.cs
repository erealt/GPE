using GPE.Datos;
using GPE.Logica;
using System.Data.Common;

namespace GPE.Escritorio;

public class FormNuevaDevolucion : Form
{
    private const int FabricaProvisional = 1;

    private readonly DevolucionServicio servicio = new();

    public int IdCreado { get; private set; }

    // Datos cargados de la base de datos
    private List<Producto> productos = new();
    private bool cargaCorrecta;

    private readonly TextBox txtCodigoCliente = new();
    private readonly ComboBox cmbCliente = new();
    private readonly DateTimePicker dtpSolicitud = new();
    private readonly ComboBox cmbMotivo = new();
    private readonly TextBox txtObservaciones = new();

    private record FilaProducto(
        TableLayoutPanel Panel,
        ComboBox Producto,
        NumericUpDown Cantidad,
        TextBox Lote);

    private readonly CamposFormulario campos = new();
    private readonly Label lblErrorProductos = CamposFormulario.EtiquetaError();

    private readonly Label lblResumenProductos = new();
    private readonly TableLayoutPanel contenedorFilas = new();
    private readonly List<FilaProducto> filas = new();

    public FormNuevaDevolucion()
    {
        Text = "Nueva devolución";
        Size = new Size(1200, 750);
        MinimumSize = new Size(1150, 600);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;

        PrepararControles();

        Controls.Add(CrearContenido());
    }

    // Preparación de controles

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
            Mensajes.ErrorBaseDatos(this, "No se pudieron cargar los clientes y productos de la base de datos.", ex);
        }

        cmbMotivo.Items.AddRange(new object[]
            { "Producto dañado", "Error en pedido", "Caducidad próxima", "Envase defectuoso", "Otro" });

        dtpSolicitud.Format = DateTimePickerFormat.Short;
        dtpSolicitud.MaxDate = DateTime.Today;   // no se admiten solicitudes futuras

        // El código y el cliente van a la par: al escribir el código se elige el cliente y al revés
        txtCodigoCliente.PlaceholderText = "Escribe el código del cliente";
        txtCodigoCliente.MaxLength = 20;   // CLIENTE.codigo es NVARCHAR(20)
        txtCodigoCliente.TextChanged += (s, e) =>
        {
            if (!txtCodigoCliente.Focused) return;   // el cambio viene del desplegable
            string codigo = txtCodigoCliente.Text.Trim();
            cmbCliente.SelectedItem = cmbCliente.Items.Cast<Cliente>()
                .FirstOrDefault(c => c.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase));
        };
        cmbCliente.SelectionChangeCommitted += (s, e) =>
            txtCodigoCliente.Text = (cmbCliente.SelectedItem as Cliente)?.Codigo ?? "";
        txtObservaciones.PlaceholderText = "Añade cualquier información relevante sobre la solicitud...";
        txtObservaciones.Multiline = true;
        txtObservaciones.Height = 80;
        txtObservaciones.ScrollBars = ScrollBars.Vertical;
    }

    // Estructura general

    private Panel CrearContenido()
    {
        var (scroll, pila) = Componentes.Pagina();

        pila.Controls.Add(Componentes.Cabecera(
            "DEVOLUCIONES / NUEVA",
            "Nueva devolución",
            "Completa los datos para iniciar el proceso de devolución."));
        pila.Controls.Add(CrearTarjetaDatos());
        pila.Controls.Add(CrearTarjetaProductos());
        pila.Controls.Add(CrearAcciones());

        return scroll;
    }

    // Tarjeta 1: datos

    private Control CrearTarjetaDatos()
    {
        var tarjeta = Componentes.Tarjeta("1   Datos de la devolución", "Información general de la solicitud del cliente");

        var rejilla = Componentes.Rejilla(2);
        rejilla.Controls.Add(campos.Campo("Código de cliente", txtCodigoCliente));
        rejilla.Controls.Add(campos.Campo("Cliente", cmbCliente));
        rejilla.Controls.Add(campos.Campo("Fecha de solicitud", dtpSolicitud));
        rejilla.Controls.Add(campos.Campo("Motivo de devolución", cmbMotivo));

        var campoObservaciones = campos.Campo("Observaciones", txtObservaciones);
        rejilla.Controls.Add(campoObservaciones);
        rejilla.SetColumnSpan(campoObservaciones, 2);

        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Tarjeta: productos esperados

    private Control CrearTarjetaProductos()
    {
        var btnAnadir = Tema.BotonSecundario("+  Añadir producto", 190);
        btnAnadir.Click += (s, e) => AnadirFilaProducto();

        var tarjeta = Componentes.Tarjeta("2   Productos esperados (opcional)",
            "Si todavía no se sabe qué va a devolver el cliente, déjalo vacío.", btnAnadir, lblResumenProductos);

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
        var btnGuardar = Tema.BotonPrincipal("Guardar devolución", 190);
        btnGuardar.Enabled = cargaCorrecta;
        btnGuardar.Click += (s, e) => Guardar();

        var btnCancelar = Tema.BotonSecundario("Cancelar", 130);
        btnCancelar.DialogResult = DialogResult.Cancel;
        CancelButton = btnCancelar;   // Esc cierra sin guardar

        return Componentes.Acciones(btnGuardar, btnCancelar);
    }

    // Guardado

    private void Guardar()
    {
        // Se marcan todos los campos con error a la vez
        LimpiarErrores();
        Control? primerError = null;

        if (cmbCliente.SelectedItem is not Cliente)
        {
            if (txtCodigoCliente.Text.Trim() != "")
            {
                campos.MarcarError(txtCodigoCliente, "No hay ningún cliente con ese código.");
                primerError ??= txtCodigoCliente;
            }
            else
            {
                campos.MarcarError(cmbCliente, "Indica el código o selecciona un cliente.");
                primerError ??= cmbCliente;
            }
        }

        if (cmbMotivo.SelectedItem is not string)
        {
            campos.MarcarError(cmbMotivo, "Selecciona el motivo de la devolución.");
            primerError ??= cmbMotivo;
        }

        // Los productos son opcionales y las filas vacías se ignoran,
        // pero una fila con lote tiene que llevar producto
        var filasSinProducto = filas
            .Where(f => f.Producto.SelectedItem is not Producto && !string.IsNullOrWhiteSpace(f.Lote.Text))
            .ToList();
        if (filasSinProducto.Count > 0)
        {
            MostrarErrorProductos("Hay filas con lote pero sin producto: elige uno o quítalas con ✕.");
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

        var datos = new DatosNuevaDevolucion(
            FabricaProvisional,
            cliente.Id,
            motivo,
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
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar la devolución.", ex);
            return;
        }

        DialogResult = DialogResult.OK;
    }

    // Mensajes de error

    private void MostrarErrorProductos(string mensaje)
    {
        lblErrorProductos.Text = mensaje;
        lblErrorProductos.Visible = true;
    }

    private void LimpiarErrores()
    {
        campos.LimpiarErrores();
        lblErrorProductos.Visible = false;
    }

    // Filas de producto

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
                ForeColor = Tema.Suave,
                Font = Tema.FuenteEtiqueta
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
        var txtLote = new TextBox { PlaceholderText = "Lote", MaxLength = 30 };   // DEVOLUCION_PRODUCTO.lote es NVARCHAR(30)

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
            ? "Sin productos indicados"
            : $"{conProducto.Count} {(conProducto.Count == 1 ? "producto" : "productos")} · {unidades} unidades";
    }
}