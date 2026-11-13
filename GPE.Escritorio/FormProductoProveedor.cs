using System.Data.Common;
using GPE.Datos;
using GPE.Logica;

namespace GPE.Escritorio;

// Ficha de un producto de un proveedor, nuevo o ya guardado. El muestreo y el riesgo de fraude se calculan
// con las notas del producto y las del proveedor (las que hay escritas en su ficha).
public class FormProductoProveedor : Form
{
    private const string Activo = "Activo";
    private const string DeBaja = "De baja";
    private const string SinNota = FormProveedor.SinNota;

    private readonly ProveedorServicio servicio = new();
    private readonly CamposFormulario campos = new();
    private readonly Proveedor proveedor;
    private readonly ProductoProveedor original;
    private bool hayCambios;

    private readonly TextBox txtCodigo = new();
    private readonly TextBox txtDescripcion = new();
    private readonly ComboBox cmbEstado = new();
    private readonly ComboBox cmbTipo = new();
    private readonly DateTimePicker dtpAlta = new();
    private readonly DateTimePicker dtpRevision = new();
    private readonly TextBox txtAlergenos = new();
    private readonly TextBox txtPosibles = new();

    private readonly TextBox txtFicha = new();
    private readonly TextBox txtQuimicos = new();
    private readonly TextBox txtMicro = new();
    private readonly TextBox txtAnalisisAlergenos = new();
    private readonly DateTimePicker dtpEurochoc = new();
    private readonly TextBox txtEurochocAlcance = new();
    private readonly TextBox txtEurochocResultado = Multilinea();

    private readonly ComboBox cmbImpacto;
    private readonly ComboBox cmbControl = FormProveedor.ComboNota(1, 3);
    private readonly ComboBox cmbReclamaciones = FormProveedor.ComboNota(0, 2);
    private readonly TextBox txtParametro = Multilinea();
    private readonly Label lblMuestreo = new();
    private readonly Label lblAlcance = new();

    private readonly ComboBox cmbPHistorial = FormProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbPMotivacion = FormProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbPFacilidad = FormProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbDOrigen = FormProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbDCadena = FormProveedor.ComboNota(0, 3);
    private readonly ComboBox cmbDMedidas = FormProveedor.ComboNota(0, 3);
    private readonly TextBox txtMedidas = Multilinea();
    private readonly Label lblRiesgo = new();
    private readonly Label lblActuacion = new();

    private readonly TextBox txtObservaciones = Multilinea();

    public int IdGuardado { get; private set; }

    // producto == null: producto nuevo de este proveedor
    public FormProductoProveedor(Proveedor proveedor, ProductoProveedor? producto)
    {
        this.proveedor = proveedor;
        bool nuevo = producto == null;
        original = producto ?? new ProductoProveedor(0, proveedor.Id, null, null, null, DateTime.Today, false, null, null,
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null,
            null, null, null);
        cmbImpacto = FormProveedor.ComboNota(1, 3, $"Igual que el proveedor ({proveedor.Impacto?.ToString() ?? "—"})");

        Text = nuevo ? $"Nuevo producto - {proveedor.Nombre}" : $"Producto {original.Codigo} - {original.Descripcion}";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(1000, 650);
        Componentes.AjustarAPantalla(this, 0.8);
        BackColor = Tema.Fondo;
        Font = Tema.Fuente;
        KeyPreview = true;
        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Close(); };

        var (scroll, pila) = Componentes.Pagina();
        string ruta = $"PROVEEDORES  ·  Nº {proveedor.Numero?.ToString() ?? "—"}  ·  {proveedor.Nombre}";
        pila.Controls.Add(nuevo
            ? Componentes.Cabecera(ruta, "Nuevo producto", "Rellena sus datos y guárdalo.")
            : Componentes.Cabecera(ruta, original.Descripcion ?? original.Codigo ?? "Producto",
                string.Join("  ·  ", new[] { original.Codigo, original.TipoProducto, original.Baja ? "De baja" : "Activo" }.OfType<string>())));

        pila.Controls.Add(Marcada(TarjetaDatos(), EstiloProveedores.RayaDatos));
        pila.Controls.Add(Marcada(TarjetaDocumentacion(), EstiloProveedores.RayaProductos));
        pila.Controls.Add(Marcada(TarjetaMuestreo(), EstiloProveedores.RayaEvaluacion));
        pila.Controls.Add(Marcada(TarjetaFraude(), EstiloProveedores.RayaRiesgo));
        var observaciones = Componentes.Tarjeta("Observaciones");
        txtObservaciones.Dock = DockStyle.Top;
        txtObservaciones.Height = 80;
        observaciones.Controls.Add(txtObservaciones);
        pila.Controls.Add(observaciones);

        var btnGuardar = Tema.BotonPrincipal(nuevo ? "Guardar producto" : "Guardar cambios", 190);
        btnGuardar.Click += (s, e) => Guardar();
        var btnCancelar = Tema.BotonSecundario("Cancelar", 140);
        btnCancelar.Click += (s, e) => Close();
        pila.Controls.Add(Componentes.Acciones(btnGuardar, btnCancelar));

        Controls.Add(scroll);

        Poner(original);
        ActualizarNotas();

        foreach (var control in FormProveedor.Entradas(this))
        {
            EventHandler cambio = (s, e) => { hayCambios = true; ActualizarNotas(); };
            switch (control)
            {
                case ComboBox combo: combo.SelectedIndexChanged += cambio; break;
                case DateTimePicker fecha: fecha.ValueChanged += cambio; break;
                default: control.TextChanged += cambio; break;
            }
        }
        FormClosing += AvisarCambiosSinGuardar;

        Shown += (s, e) =>
        {
            scroll.AutoScrollPosition = Point.Empty;
            (nuevo ? txtCodigo : txtDescripcion).Focus();
        };
    }

    private static Control Marcada(Control tarjeta, Color color)
    {
        EstiloProveedores.MarcarTarjeta(tarjeta, color);
        return tarjeta;
    }

    private static TextBox Multilinea() => new() { Multiline = true, Height = 60, ScrollBars = ScrollBars.Vertical };

    private static Control Ancho(TableLayoutPanel rejilla, Control campo, int columnas)
    {
        rejilla.Controls.Add(campo);
        rejilla.SetColumnSpan(campo, columnas);
        return campo;
    }

    // Tarjetas

    private Control TarjetaDatos()
    {
        var tarjeta = Componentes.Tarjeta("Datos del producto");
        var rejilla = Componentes.Rejilla(4);

        cmbEstado.Items.AddRange(new object[] { Activo, DeBaja });
        cmbTipo.Items.Add(SinNota);
        cmbTipo.Items.AddRange(TipoProductoProveedor.Todos);
        if (original.TipoProducto != null && !cmbTipo.Items.Contains(original.TipoProducto))
            cmbTipo.Items.Add(original.TipoProducto);
        foreach (var combo in new[] { cmbEstado, cmbTipo })
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
        foreach (var fecha in new[] { dtpAlta, dtpRevision, dtpEurochoc })
        {
            fecha.Format = DateTimePickerFormat.Short;
            fecha.ShowCheckBox = true;   // sin marcar = sin fecha
        }

        rejilla.Controls.Add(campos.Campo("Código", txtCodigo));
        Ancho(rejilla, campos.Campo("Descripción", txtDescripcion), 2);
        rejilla.Controls.Add(campos.Campo("Estado", cmbEstado));
        rejilla.Controls.Add(campos.Campo("Tipo producto", cmbTipo));
        rejilla.Controls.Add(campos.Campo("Fecha alta", dtpAlta));
        rejilla.Controls.Add(campos.Campo("Fecha revisión", dtpRevision));
        rejilla.Controls.Add(new Panel { Size = Size.Empty, Margin = Padding.Empty });   // salta al principio de la fila
        Ancho(rejilla, campos.Campo("Alérgenos", txtAlergenos), 2);
        Ancho(rejilla, campos.Campo("Posibles alérgenos", txtPosibles), 2);
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control TarjetaDocumentacion()
    {
        var tarjeta = Componentes.Tarjeta("Documentación y análisis",
            "Se puede escribir una fecha, un año o una explicación.");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("Ficha técnica", txtFicha));
        rejilla.Controls.Add(campos.Campo("Análisis químicos / Decl. conformidad", txtQuimicos));
        rejilla.Controls.Add(campos.Campo("Análisis micro / Ensayo migración", txtMicro));
        rejilla.Controls.Add(campos.Campo("Análisis alérgenos / Formaldehído", txtAnalisisAlergenos));
        rejilla.Controls.Add(campos.Campo("Fecha análisis Eurochoc", dtpEurochoc));
        Ancho(rejilla, campos.Campo("Alcance Eurochoc", txtEurochocAlcance), 3);
        Ancho(rejilla, campos.Campo("Resultado Eurochoc", txtEurochocResultado), 4);
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control TarjetaMuestreo()
    {
        var tarjeta = Componentes.Tarjeta("Frecuencia de muestreo",
            "Impacto + estatus del proveedor + control recepción + reclamaciones: 1-3 A, 4-6 B, 7-9 C y 10-12 D.");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("Impacto del producto", cmbImpacto));
        rejilla.Controls.Add(campos.Campo("Control recepción", cmbControl));
        rejilla.Controls.Add(campos.Campo("Reclamaciones", cmbReclamaciones));
        rejilla.Controls.Add(Resultado("Muestreo", lblMuestreo, lblAlcance));
        Ancho(rejilla, campos.Campo("Parámetro", txtParametro), 4);
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    private Control TarjetaFraude()
    {
        var tarjeta = Componentes.Tarjeta("Vulnerabilidad al fraude",
            "La ocurrencia (P.) más alta por la detección (D.) más alta, por el riesgo del proveedor: 1-6 BAJO, 7-12 MEDIO y 13-27 ALTO.");
        var rejilla = Componentes.Rejilla(4);
        rejilla.Controls.Add(campos.Campo("P. historial", cmbPHistorial));
        rejilla.Controls.Add(campos.Campo("P. motivación económica", cmbPMotivacion));
        rejilla.Controls.Add(campos.Campo("P. facilidad", cmbPFacilidad));
        var resultado = Resultado("Riesgo de fraude", lblRiesgo, lblActuacion);
        rejilla.Controls.Add(resultado);
        rejilla.SetRowSpan(resultado, 2);
        rejilla.Controls.Add(campos.Campo("D. origen", cmbDOrigen));
        rejilla.Controls.Add(campos.Campo("D. cadena suministro", cmbDCadena));
        rejilla.Controls.Add(campos.Campo("D. medidas control", cmbDMedidas));
        Ancho(rejilla, campos.Campo("Medidas preventivas", txtMedidas), 4);
        tarjeta.Controls.Add(rejilla);
        return tarjeta;
    }

    // Nota calculada (etiqueta de color) con una explicación debajo
    private static Control Resultado(string etiqueta, Label nota, Label explicacion)
    {
        var dato = FormProveedor.PanelDato(etiqueta);
        nota.AutoSize = false;
        nota.Size = new Size(80, 28);
        nota.TextAlign = ContentAlignment.MiddleCenter;
        nota.Font = Tema.FuenteNegrita;
        nota.Margin = new Padding(0, 4, 0, 0);
        explicacion.AutoSize = true;
        explicacion.ForeColor = Tema.Suave;
        explicacion.Font = Tema.FuenteNota;
        explicacion.MaximumSize = new Size(280, 0);
        explicacion.Margin = new Padding(0, 4, 0, 0);
        dato.Controls.Add(nota);
        dato.Controls.Add(explicacion);
        return dato;
    }

    // Leer y poner

    private void Poner(ProductoProveedor x)
    {
        txtCodigo.Text = x.Codigo ?? "";
        txtDescripcion.Text = x.Descripcion ?? "";
        cmbEstado.SelectedItem = x.Baja ? DeBaja : Activo;
        cmbTipo.SelectedItem = x.TipoProducto ?? SinNota;
        FormProveedor.PonerFecha(dtpAlta, x.FechaAlta);
        FormProveedor.PonerFecha(dtpRevision, x.FechaRevision);
        txtAlergenos.Text = x.Alergenos ?? "";
        txtPosibles.Text = x.PosiblesAlergenos ?? "";
        txtFicha.Text = x.FichaTecnica ?? "";
        txtQuimicos.Text = x.AnalisisQuimicos ?? "";
        txtMicro.Text = x.AnalisisMicro ?? "";
        txtAnalisisAlergenos.Text = x.AnalisisAlergenos ?? "";
        FormProveedor.PonerFecha(dtpEurochoc, x.EurochocFecha);
        txtEurochocAlcance.Text = x.EurochocAlcance ?? "";
        txtEurochocResultado.Text = x.EurochocResultado ?? "";
        FormProveedor.PonerNota(cmbImpacto, x.Impacto);
        FormProveedor.PonerNota(cmbControl, x.ControlRecepcion);
        FormProveedor.PonerNota(cmbReclamaciones, x.Reclamaciones);
        txtParametro.Text = x.Parametro ?? "";
        FormProveedor.PonerNota(cmbPHistorial, x.PHistorial);
        FormProveedor.PonerNota(cmbPMotivacion, x.PMotivacion);
        FormProveedor.PonerNota(cmbPFacilidad, x.PFacilidad);
        FormProveedor.PonerNota(cmbDOrigen, x.DOrigen);
        FormProveedor.PonerNota(cmbDCadena, x.DCadena);
        FormProveedor.PonerNota(cmbDMedidas, x.DMedidas);
        txtMedidas.Text = x.MedidasPreventivas ?? "";
        txtObservaciones.Text = x.Observaciones ?? "";
    }

    private ProductoProveedor Leer() => original with
    {
        Codigo = txtCodigo.Text,
        Descripcion = txtDescripcion.Text,
        Baja = cmbEstado.SelectedItem as string == DeBaja,
        TipoProducto = cmbTipo.SelectedItem as string is string tipo && tipo != SinNota ? tipo : null,
        FechaAlta = FormProveedor.LeerFecha(dtpAlta),
        FechaRevision = FormProveedor.LeerFecha(dtpRevision),
        Alergenos = txtAlergenos.Text,
        PosiblesAlergenos = txtPosibles.Text,
        FichaTecnica = txtFicha.Text,
        AnalisisQuimicos = txtQuimicos.Text,
        AnalisisMicro = txtMicro.Text,
        AnalisisAlergenos = txtAnalisisAlergenos.Text,
        EurochocFecha = FormProveedor.LeerFecha(dtpEurochoc),
        EurochocAlcance = txtEurochocAlcance.Text,
        EurochocResultado = txtEurochocResultado.Text,
        Impacto = FormProveedor.LeerNota(cmbImpacto),
        ControlRecepcion = FormProveedor.LeerNota(cmbControl),
        Reclamaciones = FormProveedor.LeerNota(cmbReclamaciones),
        Parametro = txtParametro.Text,
        PHistorial = FormProveedor.LeerNota(cmbPHistorial),
        PMotivacion = FormProveedor.LeerNota(cmbPMotivacion),
        PFacilidad = FormProveedor.LeerNota(cmbPFacilidad),
        DOrigen = FormProveedor.LeerNota(cmbDOrigen),
        DCadena = FormProveedor.LeerNota(cmbDCadena),
        DMedidas = FormProveedor.LeerNota(cmbDMedidas),
        MedidasPreventivas = txtMedidas.Text,
        Observaciones = txtObservaciones.Text
    };

    // Muestreo y riesgo con lo que hay escrito
    private void ActualizarNotas()
    {
        var x = Leer();

        string? muestreo = ProveedorServicio.FrecuenciaMuestreo(proveedor, x);
        Pintar(lblMuestreo, muestreo);
        lblAlcance.Text = muestreo == null
            ? "Falta el impacto, el estatus del proveedor o el control en recepción"
            : $"{N(ProveedorServicio.ImpactoProducto(proveedor, x))} + {N(proveedor.Estatus)} + {N(x.ControlRecepcion)} + {N(x.Reclamaciones)}. "
              + ProveedorServicio.AlcanceMuestreo(muestreo);

        string? riesgo = ProveedorServicio.RiesgoFraude(proveedor, x);
        Pintar(lblRiesgo, riesgo);
        lblActuacion.Text = riesgo == null
            ? "Faltan las notas de ocurrencia y detección, o el riesgo del proveedor"
            : $"O × D = {ProveedorServicio.OcurrenciaPorDeteccion(x)} (vulnerabilidad {ProveedorServicio.RiesgoVulnerabilidad(x)}), "
              + $"puntuación {ProveedorServicio.Puntuacion(proveedor, x)}. {ProveedorServicio.Actuacion(riesgo)}";
    }

    private static string N(byte? nota) => nota?.ToString() ?? "0";

    private static void Pintar(Label etiqueta, string? nota)
    {
        var (fondo, color) = Tema.ColorNotaProveedor(nota ?? "");
        etiqueta.Text = nota ?? "—";
        etiqueta.BackColor = fondo;
        etiqueta.ForeColor = color;
    }

    // Guardado

    private void Guardar()
    {
        campos.LimpiarErrores();
        var leido = ProveedorServicio.Normalizar(Leer());

        var controles = new Dictionary<string, Control>
        {
            [nameof(ProductoProveedor.Codigo)] = txtCodigo,
            [nameof(ProductoProveedor.Descripcion)] = txtDescripcion,
            [nameof(ProductoProveedor.Alergenos)] = txtAlergenos,
            [nameof(ProductoProveedor.PosiblesAlergenos)] = txtPosibles,
            [nameof(ProductoProveedor.FichaTecnica)] = txtFicha,
            [nameof(ProductoProveedor.AnalisisQuimicos)] = txtQuimicos,
            [nameof(ProductoProveedor.AnalisisMicro)] = txtMicro,
            [nameof(ProductoProveedor.AnalisisAlergenos)] = txtAnalisisAlergenos,
            [nameof(ProductoProveedor.EurochocAlcance)] = txtEurochocAlcance,
            [nameof(ProductoProveedor.EurochocResultado)] = txtEurochocResultado,
            [nameof(ProductoProveedor.Parametro)] = txtParametro,
            [nameof(ProductoProveedor.MedidasPreventivas)] = txtMedidas
        };
        Control? primerError = null;
        foreach (var (campo, mensaje) in ProveedorServicio.ErroresProducto(leido))
        {
            campos.MarcarError(controles[campo], mensaje);
            primerError ??= controles[campo];
        }
        if (primerError != null)
        {
            primerError.Focus();
            return;
        }

        try
        {
            IdGuardado = servicio.GuardarProducto(leido);
        }
        catch (ReglaNegocioException ex)
        {
            Mensajes.Aviso(this, "No se puede guardar", ex.Message);
            return;
        }
        catch (DbException ex)
        {
            Mensajes.ErrorBaseDatos(this, "No se pudo guardar el producto.", ex);
            return;
        }

        hayCambios = false;
        DialogResult = DialogResult.OK;
    }

    private void AvisarCambiosSinGuardar(object? sender, FormClosingEventArgs e)
    {
        if (!hayCambios || DialogResult == DialogResult.OK) return;

        if (!Mensajes.DescartarCambios(this, "Hay cambios sin guardar. ¿Salir sin guardarlos?"))
            e.Cancel = true;
    }
}
